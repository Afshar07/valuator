namespace ProjectOperations.Core.Domain;

public sealed class ProjectTemplate
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<RequirementGroup> Groups { get; set; } = [];

    public List<ProjectRequirement> InstantiateRequirements() => Groups
        .SelectMany(group => group.Requirements.Select(definition => new ProjectRequirement
        {
            DefinitionId = definition.Id,
            GroupId = group.Id,
            Title = definition.Title,
            Type = definition.Type,
            Status = RequirementStatus.Missing
        })).ToList();
}

public sealed class RequirementGroup
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public List<RequirementDefinition> Requirements { get; set; } = [];
}

public sealed class RequirementDefinition
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public RequirementType Type { get; set; }
}

public static class VcTemplate
{
    public static ProjectTemplate Create() => new()
    {
        Id = "vc-investment-v1",
        Name = "VC Investment Review",
        Groups =
        [
            new() { Id = "business", Title = "اطلاعات کسب‌وکار", Requirements =
            [
                new() { Id = "pitch-deck", Title = "Pitch deck", Type = RequirementType.Document },
                new() { Id = "operations", Title = "اطلاعات عملیات اجرایی / فرآیند اجرا", Type = RequirementType.Text },
                new() { Id = "financial-forecast", Title = "فایل پیش‌بینی مالی", Type = RequirementType.Document },
                new() { Id = "requested-capital", Title = "سرمایه درخواستی و نحوه تخصیص سرمایه", Type = RequirementType.Money },
                new() { Id = "development-plan", Title = "برنامه توسعه و چرایی جذب سرمایه", Type = RequirementType.Text },
                new() { Id = "customers-contracts", Title = "تعداد مشتریان، قراردادهای فعال و قراردادهای در حال مذاکره", Type = RequirementType.Structured }
            ] },
            new() { Id = "finance", Title = "اطلاعات مالی", Requirements =
            [
                new() { Id = "profit-loss", Title = "صورت سود و زیان", Type = RequirementType.Document },
                new() { Id = "cash-flow", Title = "جریان وجوه نقد", Type = RequirementType.Document },
                new() { Id = "balance-sheet", Title = "ترازنامه", Type = RequirementType.Document },
                new() { Id = "trial-balance", Title = "تراز کل / آزمایشی", Type = RequirementType.Document },
                new() { Id = "monthly-history", Title = "سوابق فروش و هزینه‌کرد ماهانه", Type = RequirementType.Document },
                new() { Id = "fundraising-history", Title = "سوابق جذب سرمایه", Type = RequirementType.Structured }
            ] },
            new() { Id = "shareholders", Title = "اطلاعات سهامداران", Requirements =
            [
                new() { Id = "shareholders", Title = "ترکیب سهامداران", Type = RequirementType.Structured },
                new() { Id = "executive-team", Title = "ترکیب تیم اجرایی", Type = RequirementType.Structured },
                new() { Id = "available-equity", Title = "سهام آزاد برای سرمایه‌گذاری", Type = RequirementType.Number }
            ] },
            new() { Id = "supplemental", Title = "اطلاعات تکمیلی", Requirements =
            [
                new() { Id = "licenses", Title = "مجوزها", Type = RequirementType.Document }
            ] }
        ]
    };
}
