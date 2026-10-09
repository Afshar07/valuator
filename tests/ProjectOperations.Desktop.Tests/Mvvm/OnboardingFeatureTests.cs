using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Features.Onboarding;
using ProjectOperations.Desktop.Shell;
using Xunit;

namespace ProjectOperations.Desktop.Tests.Mvvm;

/// <summary>The theme and language options, the welcome screen and the new-project wizard as plain view-models.</summary>
public sealed class OnboardingFeatureTests
{
    private static WizardViewModel Wizard(ShellScenario scenario, bool fromWelcome = false)
    {
        scenario.Shell.ShowWizard(fromWelcome);
        return Assert.IsType<WizardViewModel>(scenario.Shell.Overlay);
    }

    // ---- display options -----------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Choosing_a_theme_saves_it_and_a_theme_changed_elsewhere_moves_the_chips()
    {
        using var scenario = await ShellScenario.CreateAsync();
        var display = scenario.Shell.Display;
        Assert.Equal(["light", "dark", "auto"], display.Theme.Options.Select(option => option.Label.ToLowerInvariant()));
        Assert.Equal("light", display.Theme.Selected);

        display.Theme.Options[1].SelectCommand.Execute(null);
        Assert.Equal("dark", scenario.Appearance.Theme);
        Assert.Equal("dark", scenario.Shell.ThemeId);

        scenario.Appearance.SetTheme("auto");
        Assert.Equal("auto", display.Theme.Selected);
        Assert.Equal("auto", scenario.Shell.ThemeId);
    }

    [Fact]
    public async Task A_theme_that_cannot_be_saved_is_refused_with_an_error_and_the_chips_go_back()
    {
        using var scenario = await ShellScenario.CreateAsync();
        Directory.CreateDirectory(scenario.SettingsPath);

        scenario.Shell.Display.Theme.Options[1].SelectCommand.Execute(null);

        Assert.Equal("light", scenario.Appearance.Theme);
        Assert.Equal("light", scenario.Shell.Display.Theme.Selected);
        Assert.Equal("validation.themeSaveFailed", scenario.Shell.Messages.ErrorKey);
        Assert.True(scenario.Shell.HasError);
        Assert.Equal(scenario.Strings["validation.themeSaveFailed"], scenario.Shell.ErrorText);
    }

    [Fact]
    public async Task The_language_button_names_both_languages_in_themselves_and_switches_between_them()
    {
        using var scenario = await ShellScenario.CreateAsync();
        var display = scenario.Shell.Display;
        Assert.Equal(("English", "فارسی", false), (display.CurrentLanguageName, display.OtherLanguageName, display.IsPersian));

        display.ToggleLanguageCommand.Execute(null);

        Assert.Equal("fa", scenario.Locale.LanguageCode);
        Assert.Equal(("فارسی", "English", true), (display.CurrentLanguageName, display.OtherLanguageName, display.IsPersian));
        display.ToggleLanguageCommand.Execute(null);
        Assert.Equal("en", scenario.Locale.LanguageCode);
    }

    [Fact]
    public async Task A_language_that_cannot_be_saved_is_refused_with_an_error()
    {
        using var scenario = await ShellScenario.CreateAsync();
        Directory.CreateDirectory(scenario.SettingsPath);

        scenario.Shell.Display.ToggleLanguageCommand.Execute(null);

        Assert.Equal("en", scenario.Locale.LanguageCode);
        Assert.Equal("validation.languageSaveFailed", scenario.Shell.Messages.ErrorKey);
    }

    // ---- welcome -------------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Starting_your_own_project_opens_the_wizard_with_back_returning_to_the_welcome_screen()
    {
        using var scenario = await ShellScenario.CreateAsync();
        scenario.Shell.ShowWelcome();
        var welcome = Assert.IsType<WelcomeViewModel>(scenario.Shell.Overlay);

        await welcome.StartOwnCommand.ExecuteAsync(null);
        var wizard = Assert.IsType<WizardViewModel>(scenario.Shell.Overlay);
        await wizard.BackCommand.ExecuteAsync(null);

        Assert.IsType<WelcomeViewModel>(scenario.Shell.Overlay);
    }

    [Fact]
    public async Task Exploring_the_sample_leaves_the_welcome_screen_for_the_sample_workspace()
    {
        using var scenario = await ShellScenario.CreateAsync();
        scenario.Shell.ShowWelcome();
        var welcome = Assert.IsType<WelcomeViewModel>(scenario.Shell.Overlay);

        await welcome.StartSampleCommand.ExecuteAsync(null);

        Assert.Null(scenario.Shell.Overlay);
        Assert.True(scenario.Shell.IsSample);
        Assert.Empty(await scenario.Projects.ListAsync());
    }

    // ---- wizard --------------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_wizard_needs_a_name_to_go_on_and_the_step_markers_follow_the_step()
    {
        using var scenario = await ShellScenario.CreateAsync();
        var wizard = Wizard(scenario);
        Assert.True(wizard.IsNameStep);
        Assert.False(wizard.CanContinue);
        Assert.Equal([true, false, false], wizard.Steps.Select(step => step.Reached));
        Assert.Equal([true, false, false], wizard.Steps.Select(step => step.Current));
        Assert.Equal([true, true, false], wizard.Steps.Select(step => step.HasLine));
        Assert.Equal(scenario.Strings["v3.next"], wizard.PrimaryText);

        wizard.Name = "   ";
        Assert.False(wizard.CanContinue);
        wizard.Name = "Nova";
        Assert.True(wizard.CanContinue);

        await wizard.PrimaryCommand.ExecuteAsync(null);
        Assert.True(wizard.IsTemplateStep);
        Assert.Equal([true, true, false], wizard.Steps.Select(step => step.Reached));
        Assert.Equal([true, false, false], wizard.Steps.Select(step => step.Done));
        Assert.Equal([true, false, false], wizard.Steps.Select(step => step.ShowNumber).Select(shown => !shown));
        Assert.True(wizard.CanContinue);

        await wizard.PrimaryCommand.ExecuteAsync(null);
        Assert.True(wizard.IsFirstItemStep);
        Assert.Equal(scenario.Strings["v3.openProject"], wizard.PrimaryText);
    }

    [Fact]
    public async Task Back_goes_one_step_then_to_the_welcome_screen_or_closes_the_wizard()
    {
        using var scenario = await ShellScenario.CreateAsync();
        var fromWelcome = Wizard(scenario, fromWelcome: true);
        fromWelcome.Name = "Nova";
        await fromWelcome.PrimaryCommand.ExecuteAsync(null);

        await fromWelcome.BackCommand.ExecuteAsync(null);
        Assert.True(fromWelcome.IsNameStep);
        Assert.Same(fromWelcome, scenario.Shell.Overlay);
        await fromWelcome.BackCommand.ExecuteAsync(null);
        Assert.IsType<WelcomeViewModel>(scenario.Shell.Overlay);

        var alone = Wizard(scenario, fromWelcome: false);
        await alone.BackCommand.ExecuteAsync(null);
        Assert.Null(scenario.Shell.Overlay);
    }

    [Fact]
    public async Task The_back_arrow_points_the_way_back_in_both_directions()
    {
        using var scenario = await ShellScenario.CreateAsync();
        var wizard = Wizard(scenario);
        Assert.Equal(Icons.ArrowLeft, wizard.BackIcon);

        scenario.Locale.SetLanguage("fa");

        Assert.Equal(Icons.ArrowRight, wizard.BackIcon);
    }

    [Fact]
    public async Task The_template_choice_decides_the_last_step_and_whether_the_checklist_preview_shows()
    {
        using var scenario = await ShellScenario.CreateAsync();
        var wizard = Wizard(scenario);
        wizard.Name = "Nova";
        await wizard.PrimaryCommand.ExecuteAsync(null);

        Assert.True(wizard.IsVcTemplate);
        Assert.True(wizard.ShowPreview);
        Assert.Equal([true, false], wizard.TemplateOptions.Select(option => option.IsSelected));
        Assert.Equal(["TemplateVc", "TemplateBlank"], wizard.TemplateOptions.Select(option => option.ControlName));
        Assert.Equal(4, wizard.PreviewGroups.Count);
        Assert.Equal([true, false, false, false], wizard.PreviewGroups.Select(group => group.IsFirst));
        Assert.EndsWith(" …", wizard.PreviewGroups[0].Items);
        Assert.Equal($"{scenario.Strings.Number(6)} {scenario.Strings["v3.items"]}", wizard.PreviewGroups[0].CountText);

        wizard.TemplateOptions[1].ChooseCommand.Execute(null);
        Assert.True(wizard.IsBlankTemplate);
        Assert.False(wizard.ShowPreview);
        Assert.Equal([false, true], wizard.TemplateOptions.Select(option => option.IsSelected));
        Assert.Equal(Icons.RadioButton, wizard.TemplateOptions[1].Icon);
        Assert.Equal(Icons.Circle, wizard.TemplateOptions[0].Icon);

        await wizard.PrimaryCommand.ExecuteAsync(null);
        Assert.True(wizard.ShowRequirement);
        Assert.False(wizard.ShowDeck);
        Assert.Equal(scenario.Strings["v3.w3BlankSub"], wizard.SubheadingText);
    }

    [Fact]
    public async Task Skip_is_offered_on_the_last_step_until_the_first_item_is_given()
    {
        using var scenario = await ShellScenario.CreateAsync();
        var wizard = Wizard(scenario);
        wizard.Name = "Nova";
        wizard.TemplateOptions[1].ChooseCommand.Execute(null);
        Assert.False(wizard.ShowSkip);
        await wizard.PrimaryCommand.ExecuteAsync(null);
        await wizard.PrimaryCommand.ExecuteAsync(null);

        Assert.True(wizard.ShowSkip);
        wizard.Requirement = "Customer contracts";
        Assert.False(wizard.ShowSkip);
        wizard.Requirement = "  ";
        Assert.True(wizard.ShowSkip);
    }

    [Fact]
    public async Task A_blank_project_is_created_with_its_first_requirement_and_opened_on_the_checklist()
    {
        using var scenario = await ShellScenario.CreateAsync();
        var wizard = Wizard(scenario);
        wizard.Name = "  Nova Logistics  "; wizard.Company = "Nova";
        wizard.TemplateOptions[1].ChooseCommand.Execute(null);
        await wizard.PrimaryCommand.ExecuteAsync(null);
        await wizard.PrimaryCommand.ExecuteAsync(null);
        wizard.Requirement = "Customer contracts";

        await wizard.PrimaryCommand.ExecuteAsync(null);

        var project = Assert.Single(await scenario.Projects.ListAsync());
        Assert.Equal(("Nova Logistics", "Nova", "blank"), (project.Name, project.CompanyName, project.TemplateId));
        Assert.Equal("Customer contracts", Assert.Single(project.Requirements).Title);
        Assert.Null(scenario.Shell.Overlay);
        Assert.Equal(new ProjectRoute(project.Id, ProjectTab.Checklist), scenario.Shell.Navigator.Current);
        Assert.Equal("v3.toastCreated", scenario.Shell.Messages.ToastKey);
    }

    [Fact]
    public async Task Skipping_creates_a_project_with_the_default_item_and_a_blank_name_gets_the_default_name()
    {
        using var scenario = await ShellScenario.CreateAsync();
        var wizard = Wizard(scenario);
        wizard.TemplateOptions[1].ChooseCommand.Execute(null);
        wizard.Step = 2;

        await wizard.SkipCommand.ExecuteAsync(null);

        var project = Assert.Single(await scenario.Projects.ListAsync());
        Assert.Equal(scenario.Strings["v3.defaultName"], project.Name);
        Assert.Equal(scenario.Strings["v3.defaultRequirement"], Assert.Single(project.Requirements).Title);
    }

    [Fact]
    public async Task A_vc_project_gets_the_whole_checklist_and_a_chosen_deck_is_linked_as_provided()
    {
        using var scenario = await ShellScenario.CreateAsync();
        var deck = Path.Combine(Path.GetTempPath(), "wizard-deck-" + Guid.NewGuid() + ".pdf");
        await File.WriteAllTextAsync(deck, "deck contents");
        try
        {
            var wizard = Wizard(scenario);
            wizard.Name = "Nova";
            wizard.Step = 2;
            Assert.True(wizard.ShowDeck);
            Assert.True(wizard.ShowSkip);
            Assert.False(wizard.HasDeck);
            Assert.Equal(scenario.Strings.Enum(RequirementType.Document), wizard.DeckSubtitle);

            scenario.Picker.Next = [new PickedFile("deck.pdf", deck)];
            await wizard.ChooseDeckCommand.ExecuteAsync(null);

            Assert.True(wizard.HasDeck);
            Assert.False(wizard.ShowSkip);
            Assert.Equal(Path.GetFileName(deck), wizard.DeckSubtitle);
            Assert.Equal(Icons.CheckCircle, wizard.DeckIcon);
            await wizard.PrimaryCommand.ExecuteAsync(null);

            var project = Assert.Single(await scenario.Projects.ListAsync());
            Assert.Equal(16, project.Requirements.Count);
            var pitch = project.Requirements.Single(requirement => requirement.DefinitionId == "pitch-deck");
            Assert.Equal(RequirementStatus.Provided, pitch.Status);
            var file = Assert.Single(pitch.Files);
            Assert.Equal(deck, file.Path);
            Assert.Equal("deck contents".Length, file.SizeBytes);
        }
        finally { File.Delete(deck); }
    }

    [Fact]
    public async Task Cancelling_the_file_picker_or_choosing_a_remote_file_leaves_the_deck_unlinked()
    {
        using var scenario = await ShellScenario.CreateAsync();
        var wizard = Wizard(scenario);
        wizard.Step = 2;

        scenario.Picker.Next = [];
        await wizard.ChooseDeckCommand.ExecuteAsync(null);
        scenario.Picker.Next = [new PickedFile("cloud.pdf", null)];
        await wizard.ChooseDeckCommand.ExecuteAsync(null);

        Assert.False(wizard.HasDeck);
        Assert.Equal("validation.localFilesOnly", scenario.Shell.Messages.ErrorKey);
    }

    [Fact]
    public async Task Starting_a_real_project_from_the_sample_leaves_the_sample_behind()
    {
        using var scenario = await ShellScenario.CreateAsync();
        await scenario.Shell.StartSampleAsync();
        Assert.True(scenario.Shell.IsSample);
        var wizard = Wizard(scenario);
        wizard.Name = "Mine";
        wizard.Step = 2;

        await wizard.SkipCommand.ExecuteAsync(null);

        Assert.False(scenario.Shell.IsSample);
        Assert.Null(scenario.Shell.SampleBanner);
        Assert.Equal("Mine", Assert.Single(await scenario.Projects.ListAsync()).Name);
    }

    [Fact]
    public async Task The_wizard_text_follows_a_language_switch_and_keeps_what_was_typed()
    {
        using var scenario = await ShellScenario.CreateAsync();
        var wizard = Wizard(scenario);
        wizard.Name = "Nova";
        var english = wizard.HeadingText;

        scenario.Locale.SetLanguage("fa");

        Assert.NotEqual(english, wizard.HeadingText);
        Assert.Equal(scenario.Strings["v3.w1Title"], wizard.HeadingText);
        Assert.Equal("Nova", wizard.Name);
        Assert.Equal(scenario.Strings.Number(1), wizard.Steps[0].NumberText);
    }
}
