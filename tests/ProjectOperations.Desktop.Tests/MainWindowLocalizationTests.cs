using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ProjectOperations.Core.Domain;
using ProjectOperations.Core.Agents;
using ProjectOperations.Desktop.Localization;
using Xunit;

namespace ProjectOperations.Desktop.Tests;

public sealed class MainWindowLocalizationTests
{
    [AvaloniaFact]
    public async Task Switching_language_updates_labels_and_inherited_direction_without_discarding_edits()
    {
        using var fixture = new MainWindowTests.Fixture();
        await fixture.InitializeAsync();
        var window = fixture.Window;
        window.Show();
        var englishFont = window.FontFamily;
        await UntilAsync(() => Buttons(window).Any(button => MainWindowTests.ButtonText(button) == "Create project" && button.IsEffectivelyEnabled));
        Click(window, "Create project");
        var inputs = Controls<TextBox>(window).Where(input => input.IsEffectivelyEnabled && !input.IsReadOnly).ToList();
        inputs[0].Text = "Unsaved synthetic project";
        var stage = Controls<ComboBox>(window).Single(control => control.SelectedItem is ProjectStage);
        stage.SelectedItem = ProjectStage.DueDiligence;
        var language = Controls<ComboBox>(window).Single(control => control.Name == "LanguageSelector");
        language.SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(FlowDirection.RightToLeft, window.FlowDirection);
        Assert.Equal(FlowDirection.RightToLeft, inputs[0].FlowDirection);
        Assert.Equal("IRANYekanX", window.FontFamily.Name);
        Assert.Equal("IRANYekanX", new Typeface(window.FontFamily).GlyphTypeface.FamilyName);
        Assert.Contains("IRANYekanX", new Typeface(window.FontFamily, weight: FontWeight.Bold).GlyphTypeface.FamilyName);
        Assert.Equal(window.FontFamily, inputs[0].FontFamily);
        Assert.Equal(window.FontFamily, Controls<TextBlock>(window).Single(block => block.Text == "نام پروژه *").FontFamily);
        foreach (var weight in new[] { "Regular", "Bold" })
            Assert.True(Avalonia.Platform.AssetLoader.Exists(new Uri($"avares://ProjectOperations.Desktop/Assets/Fonts/IRANYekanX/IRANYekanX-{weight}.ttf")));
        Assert.Equal(TextAlignment.Start, inputs[0].TextAlignment);
        Assert.Equal("Unsaved synthetic project", inputs[0].Text);
        Assert.Equal(ProjectStage.DueDiligence, stage.SelectedItem);
        Assert.Contains(Controls<TextBlock>(window), block => block.Text == "نام پروژه *" && block.TextAlignment == TextAlignment.Start);
        Assert.Contains(Buttons(window), button => MainWindowTests.ButtonText(button) == "ایجاد پروژه");
        Assert.Empty(await fixture.Projects.ListAsync());
        language.SelectedIndex = 0;
        Assert.Equal(FlowDirection.LeftToRight, window.FlowDirection);
        Assert.Equal(FlowDirection.LeftToRight, inputs[0].FlowDirection);
        Assert.Equal(englishFont, window.FontFamily);
        Assert.Equal(englishFont, inputs[0].FontFamily);
        Assert.Contains(Controls<TextBlock>(window), block => block.Text == "Project name *");
        Assert.Equal("Unsaved synthetic project", inputs[0].Text);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Existing_data_and_date_editor_remain_neutral_and_consent_survives_switch()
    {
        using var fixture = new MainWindowTests.Fixture();
        await fixture.InitializeAsync();
        var project = await fixture.Projects.CreateAsync("Existing project", "Company", ProjectStage.Portfolio, ProjectStatus.Active, "", "");
        var date = new DateTimeOffset(2030, 1, 15, 9, 30, 0, TimeSpan.Zero);
        project.Tasks.Add(new ProjectTask { ProjectId = project.Id, Title = "Existing task", DueAt = date, Status = ProjectTaskStatus.InProgress });
        project.Requirements[0].Value = "Original requirement value";
        await fixture.Projects.SaveAsync(project);
        var window = fixture.Window;
        window.Show();
        await UntilAsync(() => Buttons(window).Any(button => MainWindowTests.ButtonText(button).StartsWith("Existing project ·") && button.IsEffectivelyEnabled));
        ClickPrefix(window, "Existing project ·");
        await UntilAsync(() => Controls<TabControl>(window).Any() && Buttons(window).First(button => MainWindowTests.ButtonText(button) == "All projects").IsEffectivelyEnabled);
        var tabs = Controls<TabControl>(window).Single();
        tabs.SelectedIndex = 2;
        Dispatcher.UIThread.RunJobs();
        ClickPrefix(window, "Existing task ·");
        var due = Controls<TextBox>(window).Single(control => control.Text == new LocaleDateFormatter(fixture.Locale).Edit(date));
        var status = Controls<ComboBox>(window).Single(control => control.SelectedItem is ProjectTaskStatus);
        due.Text = "2031-02-03 10:15";
        fixture.Locale.SetLanguage("fa");
        Assert.Equal(2, tabs.SelectedIndex);
        Assert.Equal("2031-02-03 10:15", due.Text);
        Assert.Equal(FlowDirection.LeftToRight, due.FlowDirection);
        Assert.Equal(TextAlignment.Start, due.TextAlignment);
        Assert.True(due.MinHeight > 0);
        Assert.Equal(ProjectTaskStatus.InProgress, status.SelectedItem);
        tabs.SelectedIndex = 0;
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Assert.Contains(Controls<TextBlock>(window), control => control.Text == "نام پروژه *");
        var stage = Controls<ComboBox>(window).Single(control => control.SelectedItem is ProjectStage);
        Assert.Equal(ProjectStage.Portfolio, stage.SelectedItem);
        Assert.Contains(stage.GetVisualDescendants().OfType<TextBlock>(), control => control.Text == "پرتفوی");
        tabs.SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(Buttons(window), button => MainWindowTests.ButtonText(button).StartsWith("ارائه معرفی کسب‌وکار ·"));
        fixture.Locale.SetLanguage("en");
        tabs.SelectedIndex = 0;
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Assert.Contains(Controls<TextBlock>(window), control => control.Text == "Project name *");
        Assert.Contains(stage.GetVisualDescendants().OfType<TextBlock>(), control => control.Text == "Portfolio");
        tabs.SelectedIndex = 2;
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Assert.Equal("2031-02-03 10:15", due.Text);
        Assert.Contains(Controls<TextBlock>(window), control => control.Text == "Task title *");
        Assert.Contains(status.GetVisualDescendants().OfType<TextBlock>(), control => control.Text == "In progress");
        fixture.Locale.SetLanguage("en");
        tabs.SelectedIndex = 3;
        Dispatcher.UIThread.RunJobs();
        var promptLabel = Controls<TextBlock>(window).Single(block => block.Text == "Delegation request");
        var parent = (StackPanel)promptLabel.Parent!;
        ((TextBox)parent.Children[parent.Children.IndexOf(promptLabel) + 1]).Text = "Synthetic request";
        Click(window, "Preview context before request");
        var consent = Controls<CheckBox>(window).Single(control => control.Content is TextBlock block && block.Text!.StartsWith("I consent"));
        consent.IsChecked = true;
        var preview = Controls<TextBox>(window).Single(control => control.IsReadOnly && control.Text!.Contains("Existing project"));
        var exactContext = preview.Text;
        fixture.Locale.SetLanguage("fa");
        Assert.Equal(3, tabs.SelectedIndex);
        Assert.True(consent.IsChecked);
        Assert.Equal(exactContext, preview.Text);
        Assert.Equal(FlowDirection.LeftToRight, preview.FlowDirection);
        Assert.Equal(TextAlignment.Start, preview.TextAlignment);
        var reloaded = (await fixture.Projects.GetAsync(project.Id))!;
        Assert.Equal(date, reloaded.Tasks.Single().DueAt);
        Assert.Equal(ProjectTaskStatus.InProgress, reloaded.Tasks.Single().Status);
        Assert.Equal("Original requirement value", reloaded.Requirements[0].Value);
        Click(window, "اجرا با زمینهٔ تأییدشده");
        await UntilAsync(() => fixture.Runtime.Calls == 1);
        Assert.Equal(AgentPrompts.BuildOutputInstructions(AgentResponseLanguage.Persian), fixture.Runtime.LastRequest!.SystemInstructions);
        Assert.False(Controls<ComboBox>(window).Single(control => control.Name == "LanguageSelector").IsEffectivelyEnabled);
        fixture.Runtime.Release.TrySetResult();
        await UntilAsync(() => Controls<ComboBox>(window).Single(control => control.Name == "LanguageSelector").IsEffectivelyEnabled);
        fixture.Runtime.Release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Click(window, "پیش‌نمایش زمینه پیش از درخواست");
        consent.IsChecked = true;
        Click(window, "اجرا با زمینهٔ تأییدشده");
        await UntilAsync(() => fixture.Runtime.Calls == 2);
        Assert.Contains(Controls<TextBlock>(window), control => control.Text == "در حال اجرا");
        Click(window, "توقف");
        Assert.Contains(Controls<TextBlock>(window), control => control.Text == "در حال توقف — منتظر پایان لغو توسط عامل…");
        fixture.Runtime.Release.TrySetResult();
        await UntilAsync(() => Controls<ComboBox>(window).Single(control => control.Name == "LanguageSelector").IsEffectivelyEnabled);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Unwritable_settings_restore_language_selection_and_show_error()
    {
        using var fixture = new MainWindowTests.Fixture();
        await fixture.InitializeAsync();
        var path = Path.Combine(Path.GetTempPath(), "opencode", "locale-settings-directory-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        var locale = new LocaleContext(path);
        var window = new MainWindow(fixture.Projects, fixture.Agents, () => Task.CompletedTask, "Synthetic configuration", locale);
        try
        {
            window.Show();
            await UntilAsync(() => Buttons(window).Any(button => MainWindowTests.ButtonText(button) == "All projects" && button.IsEffectivelyEnabled));
            var language = Controls<ComboBox>(window).Single(control => control.Name == "LanguageSelector");
            language.SelectedIndex = 1;
            Assert.Equal("en", locale.LanguageCode);
            Assert.Equal(0, language.SelectedIndex);
            Assert.Equal(FlowDirection.LeftToRight, window.FlowDirection);
            Assert.Contains(Controls<TextBlock>(window), control => control.IsVisible && control.Text == "The language preference could not be saved.");
        }
        finally { window.Close(); Directory.Delete(path); }
    }

    [Fact]
    public void Ui_resources_have_identical_keys_and_format_placeholders()
    {
        var assembly = typeof(MainWindow).Assembly;
        Dictionary<string, string> Load(string code)
        {
            using var stream = assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(name => name.EndsWith($".ui.{code}.json")))!;
            return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
        }
        var english = Load("en"); var persian = Load("fa");
        Assert.Equal(english.Keys.Order(), persian.Keys.Order());
        foreach (var key in english.Keys)
        {
            Assert.Matches(@"^[a-z][A-Za-z0-9]*(?:\.[a-z][A-Za-z0-9]*)+$", key);
            string[] Placeholders(string text) => Regex.Matches(text, @"\{\d+(?:[^{}]*)\}").Select(match => match.Value).Order().ToArray();
            Assert.Equal(Placeholders(english[key]), Placeholders(persian[key]));
            var count = Regex.Matches(english[key], @"\{(\d+)").Select(match => int.Parse(match.Groups[1].Value)).DefaultIfEmpty(-1).Max() + 1;
            var values = Enumerable.Repeat<object>(1, count).ToArray();
            Assert.NotEmpty(string.Format(english[key], values));
            Assert.NotEmpty(string.Format(persian[key], values));
        }
    }

    private static IEnumerable<T> Controls<T>(Window window) where T : Control => window.GetLogicalDescendants().OfType<T>().Distinct();
    private static IEnumerable<Button> Buttons(Window window) => Controls<Button>(window);
    private static void Click(Window window, string text) => ClickButton(Buttons(window).First(button => MainWindowTests.ButtonText(button) == text));
    private static void ClickPrefix(Window window, string prefix) => ClickButton(Buttons(window).First(button => MainWindowTests.ButtonText(button).StartsWith(prefix)));
    private static void ClickButton(Button button)
    {
        Assert.True(button.IsEffectivelyEnabled);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }
    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); }
        Assert.True(condition());
    }
}
