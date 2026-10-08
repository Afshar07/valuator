using Microsoft.Data.Sqlite;
using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;
using ProjectOperations.Infrastructure.Persistence;

namespace ProjectOperations.Desktop;

/// <summary>
/// A throw-away workspace of four example deals for "Explore a sample deal". It lives in its own temporary database and
/// temporary placeholder files, so nothing done in it touches the user's data; leaving sample mode deletes it.
/// </summary>
internal sealed class SampleWorkspace : IDisposable
{
    private readonly string _directory;
    public ProjectService Projects { get; }
    public AgentService Agents { get; }

    private SampleWorkspace(string directory, ProjectService projects, AgentService agents) { _directory = directory; Projects = projects; Agents = agents; }

    /// <summary>The sample never calls an agent: the assistant is reported as not configured while it is shown.</summary>
    private sealed class UnavailableRuntime : IAgentRuntime
    {
        public Task<AgentResult> RunAsync(AgentRequest request, IProgress<AgentEvent> progress, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The assistant is not available in the sample workspace.");
        public Task CancelAsync(string jobId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public static async Task<SampleWorkspace> CreateAsync(bool persian)
    {
        var directory = Path.Combine(Path.GetTempPath(), "valuator-sample-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var database = Path.Combine(directory, "sample.db");
        var repository = new SqliteProjectRepository(database);
        await repository.InitializeAsync();
        var projects = new ProjectService(repository);
        var jobs = new SqliteAgentJobRepository(database);
        var workspace = new SampleWorkspace(directory, projects, new AgentService(projects, new UnavailableRuntime(), jobs));
        await workspace.SeedAsync(persian, jobs);
        return workspace;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_directory, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private async Task SeedAsync(bool fa, SqliteAgentJobRepository jobs)
    {
        string T(string en, string persian) => fa ? persian : en;
        var now = DateTimeOffset.Now;
        DateTimeOffset? Day(int? offset) => offset is { } days ? new DateTimeOffset(DateTime.Today.AddDays(days).AddHours(17), TimeZoneInfo.Local.GetUtcOffset(DateTime.Today.AddDays(days))) : null;
        string File(string name, bool exists = true)
        {
            var path = Path.Combine(_directory, "Deals", name);
            if (exists) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); System.IO.File.WriteAllBytes(path, []); }
            return path;
        }

        var stages = await Projects.ListStagesAsync(VcTemplate.Create().Id);
        var nova = await Projects.CreateAsync(T("Nova Logistics", "نوا لجستیک"), T("Nova Freight Co.", "شرکت حمل نوا"), ProjectStatus.Active, T("Sara M.", "سارا م."), "", stages[1].Id);
        var atlas = await Projects.CreateAsync(T("Atlas Health", "اطلس سلامت"), T("Atlas Medical Ltd.", "اطلس پزشکی"), ProjectStatus.Active, T("Reza K.", "رضا ک."), "", stages[2].Id);
        var orbit = await Projects.CreateAsync(T("Orbit Pay", "اوربیت پی"), T("Orbit Fintech", "اوربیت فین‌تک"), ProjectStatus.Active, T("Sara M.", "سارا م."), "", stages[0].Id);
        var kavir = await Projects.CreateAsync(T("Kavir Foods", "کویر فودز"), T("Kavir Agro Group", "گروه کشت کویر"), ProjectStatus.OnHold, T("Neda A.", "ندا ا."), "", stages[4].Id);

        // Readiness of the other three deals: that many requirements complete, in checklist order.
        foreach (var (project, done) in new[] { (atlas, 13), (orbit, 3), (kavir, 16) })
            foreach (var requirement in project.Requirements.Take(done)) requirement.Status = RequirementStatus.Complete;

        ProjectRequirement Req(Project project, string definition) => project.Requirements.Single(item => item.DefinitionId == definition);
        void Set(string definition, RequirementStatus status, string? value = null, params string[] files)
        {
            var requirement = Req(nova, definition); requirement.Status = status; requirement.LastReviewedAt = now.AddDays(-3);
            if (value is not null) requirement.Value = value;
            foreach (var name in files)
            {
                var path = File(name);
                requirement.Files.Add(new ProjectFile { FileName = name, Path = path, SizeBytes = 0, AddedAt = now.AddDays(-6) });
            }
        }
        Set("pitch-deck", RequirementStatus.Complete, null, "Nova_Deck_v4.pdf");
        Set("operations", RequirementStatus.Provided);
        Set("financial-forecast", RequirementStatus.NeedsReview, null, "FP_2026-2029.xlsx", "FP_Assumptions.xlsx");
        Set("requested-capital", RequirementStatus.Complete, "$4.5M");
        Set("development-plan", RequirementStatus.Complete);
        Set("customers-contracts", RequirementStatus.Missing);
        Set("profit-loss", RequirementStatus.Complete, null, "PL_1404.pdf");
        Set("cash-flow", RequirementStatus.Complete, null, "CashFlow_Q2.xlsx");
        Set("balance-sheet", RequirementStatus.Provided, null, "BS_1404.pdf");
        Set("trial-balance", RequirementStatus.NeedsReview, null, "TB_Shahrivar.xlsx");
        Set("monthly-history", RequirementStatus.Missing);
        Set("fundraising-history", RequirementStatus.Complete);
        Set("shareholders", RequirementStatus.Missing);
        Set("executive-team", RequirementStatus.Complete);
        Set("available-equity", RequirementStatus.Provided, "12%");
        Set("licenses", RequirementStatus.Missing);
        Req(atlas, "shareholders").Files.Add(new ProjectFile { FileName = "Atlas_CapTable.xlsx", Path = File("Atlas_CapTable.xlsx"), AddedAt = now.AddDays(-13) });
        Req(atlas, "licenses").Files.Add(new ProjectFile { FileName = "Atlas_Licenses.zip", Path = File("Atlas_Licenses.zip", exists: false), AddedAt = now.AddDays(-18) });
        Req(orbit, "pitch-deck").Files.Add(new ProjectFile { FileName = "Orbit_Deck.pdf", Path = File("Orbit_Deck.pdf"), AddedAt = now.AddDays(-5) });

        void Task(Project project, string en, string persian, int? offset, ProjectTaskStatus status, string? requirement = null) =>
            project.Tasks.Add(new ProjectTask { ProjectId = project.Id, Title = T(en, persian), DueAt = Day(offset), Status = status, RequirementId = requirement is null ? null : Req(project, requirement).Id });
        Task(nova, "Request updated cap table", "درخواست جدول سهامداری به‌روز", -4, ProjectTaskStatus.Todo, "shareholders");
        Task(nova, "Review trial balance with CFO", "بررسی تراز آزمایشی با مدیر مالی", -1, ProjectTaskStatus.InProgress, "trial-balance");
        Task(nova, "Review financial forecast assumptions", "بررسی مفروضات پیش‌بینی مالی", 3, ProjectTaskStatus.InProgress, "financial-forecast");
        Task(nova, "Draft IC pre-read", "پیش‌نویس سند پیش‌خوانی کمیته", 9, ProjectTaskStatus.Todo);
        Task(nova, "Schedule site visit", "زمان‌بندی بازدید میدانی", null, ProjectTaskStatus.Todo);
        Task(nova, "Collect pitch deck v4", "دریافت نسخهٔ ۴ ارائه", -9, ProjectTaskStatus.Done, "pitch-deck");
        Task(nova, "Intro call with founders", "تماس آشنایی با بنیان‌گذاران", -15, ProjectTaskStatus.Cancelled);
        Task(atlas, "Founder call on customer contracts", "تماس با بنیان‌گذار دربارهٔ قراردادها", 1, ProjectTaskStatus.Todo);
        Task(atlas, "Review data room access", "بررسی دسترسی اتاق داده", 5, ProjectTaskStatus.Todo);
        Task(atlas, "Reference check with hospital client", "تماس مرجع با مشتری بیمارستانی", 12, ProjectTaskStatus.Todo);
        Task(orbit, "Collect licenses and permits", "جمع‌آوری مجوزها", 6, ProjectTaskStatus.Todo);
        Task(orbit, "Screening call with founders", "تماس ارزیابی با بنیان‌گذاران", 20, ProjectTaskStatus.Todo);
        Task(kavir, "Q3 KPI report from management", "دریافت گزارش KPI فصل سوم از مدیریت", -2, ProjectTaskStatus.Todo);

        void Milestone(Project project, string en, string persian, int offset, bool done = false) =>
            project.Milestones.Add(new Milestone { ProjectId = project.Id, Title = T(en, persian), DueAt = Day(offset), IsComplete = done });
        Milestone(nova, "Financial review complete", "پایان بررسی مالی", -2, true);
        Milestone(nova, "IC pre-read", "پیش‌خوانی کمیته", 13);
        Milestone(nova, "Investment committee", "کمیتهٔ سرمایه‌گذاری", 20);
        Milestone(atlas, "IC review", "جلسهٔ کمیته سرمایه‌گذاری", 2);

        nova.State.OpenQuestions =
        [
            T("Why did Q2 gross margin drop 6 points?", "چرا حاشیهٔ سود ناخالص فصل دوم ۶ واحد کاهش یافت؟"),
            T("Is the Tabriz warehouse lease signed?", "آیا قرارداد اجارهٔ انبار تبریز امضا شده است؟"),
            T("Who holds the ESOP pool today?", "استخر سهام کارکنان اکنون در اختیار کیست؟"),
            T("Status of the transport license renewal", "وضعیت تمدید مجوز حمل‌ونقل")
        ];
        nova.State.FollowUps =
        [
            T("Send revised term sheet draft to legal", "ارسال پیش‌نویس اصلاح‌شدهٔ شرایط به حقوقی"),
            T("Ask CFO for monthly spend export", "درخواست خروجی هزینه‌کرد ماهانه از مدیر مالی"),
            T("Confirm reference call with anchor customer", "تأیید تماس مرجع با مشتری اصلی")
        ];
        nova.Notes = T("Strong unit economics in Tehran routes. Expansion plan relies on two new hubs; capital ask covers 18 months of runway.",
            "اقتصاد واحد در مسیرهای تهران قوی است. برنامهٔ توسعه به دو هاب جدید وابسته است؛ سرمایهٔ درخواستی ۱۸ ماه را پوشش می‌دهد.");
        foreach (var project in new[] { nova, atlas, orbit, kavir }) await Projects.SaveAsync(project);

        TaskProposal Proposal(string en, string persian, int offset, ProposalReviewStatus status = ProposalReviewStatus.Pending) =>
            new() { Title = T(en, persian), DueAt = Day(offset), ReviewStatus = status };
        async Task Job(string en, string persian, AgentJobStatus status, DateTimeOffset at, string error = "", params TaskProposal[] proposals) =>
            await jobs.SaveAsync(new AgentJob
            {
                ProjectId = nova.Id,
                Prompt = T(en, persian),
                Status = status,
                CreatedAt = at,
                FinishedAt = status == AgentJobStatus.Interrupted ? null : at.AddMinutes(2),
                ResultText = status == AgentJobStatus.Completed ? T("Summary of the project based on the stored information.", "خلاصهٔ پروژه بر اساس اطلاعات ذخیره‌شده.") : "",
                Error = error,
                Proposals = [.. proposals]
            });
        await Job("Find missing information", "یافتن اطلاعات ناقص", AgentJobStatus.Completed, now.AddHours(-3), "",
            Proposal("Request monthly sales and spend history", "درخواست سوابق فروش و هزینه‌کرد ماهانه", 5),
            Proposal("Ask founder for shareholder structure", "درخواست ترکیب سهامداران از بنیان‌گذار", 5),
            Proposal("Collect operating licenses", "جمع‌آوری مجوزهای فعالیت", 8));
        await Job("Prepare meeting brief", "تهیهٔ خلاصهٔ جلسه", AgentJobStatus.Cancelled, now.AddDays(-2));
        await Job("Summarize project", "خلاصهٔ پروژه", AgentJobStatus.Failed, now.AddDays(-5), "Provider unreachable");
        await Job("Extract action items", "استخراج اقدامات", AgentJobStatus.Completed, now.AddDays(-8), "",
            Proposal("Confirm site visit date", "تأیید تاریخ بازدید میدانی", -6, ProposalReviewStatus.Approved),
            Proposal("Collect signed NDA", "دریافت قرارداد محرمانگی امضاشده", -6, ProposalReviewStatus.Approved),
            Proposal("Request reference customers", "درخواست مشتریان مرجع", -6, ProposalReviewStatus.Rejected));
        await Job("What needs my attention?", "چه چیزی به توجه من نیاز دارد؟", AgentJobStatus.Interrupted, now.AddDays(-11));
    }
}
