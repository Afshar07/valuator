using System.Globalization;
using System.Text.Json;
using Avalonia.Media;
using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Localization;
using Xunit;

namespace ProjectOperations.Desktop.Tests;

public sealed class LocalizationTests
{
    [Fact]
    public void Default_is_in_memory_English_and_switching_notifies_once_per_change()
    {
        var locale = new LocaleContext();
        Assert.Equal("en", locale.LanguageCode);
        Assert.Equal("en-US", locale.Culture.Name);
        Assert.Equal(FlowDirection.LeftToRight, locale.FlowDirection);
        Assert.Equal(AgentResponseLanguage.English, locale.AgentResponseLanguage);
        var changes = 0;
        locale.Changed += (_, _) => changes++;
        locale.SetLanguage(" FA ");
        Assert.Equal("fa", locale.LanguageCode);
        Assert.Equal("fa-IR", locale.Culture.Name);
        Assert.Equal(FlowDirection.RightToLeft, locale.FlowDirection);
        Assert.Equal(AgentResponseLanguage.Persian, locale.AgentResponseLanguage);
        locale.SetLanguage("fa");
        Assert.Equal(1, changes);
        locale.SetLanguage("unsupported");
        Assert.Equal("en", locale.LanguageCode);
        Assert.Equal(2, changes);
        Assert.Equal("en", new LocaleContext().LanguageCode);
    }

    [Fact]
    public void Language_setting_round_trips_and_invalid_settings_fall_back_to_English()
    {
        var directory = Path.Combine(Path.GetTempPath(), "project-operations-localization-" + Guid.NewGuid());
        var path = Path.Combine(directory, "nested", "locale.json");
        try
        {
            Assert.Equal("en", new LocaleContext(path).LanguageCode);
            new LocaleContext(path).SetLanguage("fa");
            Assert.Equal("fa", new LocaleContext(path).LanguageCode);
            using (var settings = JsonDocument.Parse(File.ReadAllText(path)))
                Assert.Equal("fa", settings.RootElement.GetProperty("LanguageCode").GetString());
            new LocaleContext(path).SetLanguage("en");
            Assert.Equal("en", new LocaleContext(path).LanguageCode);
            foreach (var invalid in new[] { "{invalid", "null", "{}", "{\"LanguageCode\":\"fr\"}", "{\"LanguageCode\":42}" })
            {
                File.WriteAllText(path, invalid);
                Assert.Equal("en", new LocaleContext(path).LanguageCode);
            }
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Lookup_switches_immediately_and_unknown_keys_have_visible_fallback()
    {
        var locale = new LocaleContext();
        var localization = new LocalizationService(locale);
        Assert.Equal("Active", localization.Get("project.status.active"));
        Assert.Equal("[unknown.key]", localization.Get("unknown.key"));
        Assert.Equal("[missing 12]", localization.Format("missing {0}", 12));
        locale.SetLanguage("fa");
        Assert.Equal("فعال", localization.Get("project.status.active"));
        Assert.Equal("[unknown.key]", localization.Get("unknown.key"));
        locale.SetLanguage("invalid");
        Assert.Equal("Active", localization.Get("project.status.active"));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("fa")]
    public void Every_domain_enum_has_a_translation_without_changing_its_identity(string language)
    {
        var locale = new LocaleContext();
        locale.SetLanguage(language);
        var localization = new LocalizationService(locale);
        var display = new DomainDisplay(localization);
        var types = new[] { typeof(ProjectStatus), typeof(ProjectStage), typeof(RequirementType),
            typeof(RequirementStatus), typeof(ProjectTaskStatus), typeof(AgentJobStatus), typeof(ProposalReviewStatus) };
        foreach (var type in types)
            foreach (Enum value in System.Enum.GetValues(type))
            {
                var original = JsonSerializer.Serialize(value, type);
                Assert.DoesNotContain("[", display.Get(value));
                Assert.False(string.IsNullOrWhiteSpace(display.Get(value)));
                Assert.Equal(original, JsonSerializer.Serialize(value, type));
            }
        Assert.Equal(localization.Get("project.stage.dueDiligence"), display.Enum(ProjectStage.DueDiligence));
        Assert.Equal(localization.Get("requirement.status.needsReview"), display.Get(RequirementStatus.NeedsReview));
        Assert.Equal(localization.Get("task.status.todo"), display.Get(ProjectTaskStatus.Todo));
    }

    [Fact]
    public void Built_in_names_use_stable_ids_and_custom_names_and_domain_data_are_preserved()
    {
        var locale = new LocaleContext();
        var display = new DomainDisplay(new LocalizationService(locale));
        var template = VcTemplate.Create();
        var snapshot = JsonSerializer.Serialize(template);
        Assert.Equal("VC Investment Review", display.Template(template));
        Assert.Equal("Business information", display.Group(template.Groups[0]));
        var requirements = template.InstantiateRequirements();
        Assert.Equal("Financial forecast", display.Requirement(requirements.Single(r => r.DefinitionId == "financial-forecast")));
        foreach (var group in template.Groups)
        {
            Assert.DoesNotContain("[", display.Group(group));
            foreach (var definition in group.Requirements)
                Assert.DoesNotContain("[", display.Requirement(definition.Id, definition.Title));
        }
        requirements[0].Title = "User-edited built-in title";
        Assert.Equal("Pitch deck", display.Requirement(requirements[0]));
        locale.SetLanguage("fa");
        Assert.Equal("اطلاعات کسب‌وکار", display.Group(template.Groups[0]));
        foreach (var requirement in requirements) Assert.DoesNotContain("[", display.Requirement(requirement));
        Assert.Equal(snapshot, JsonSerializer.Serialize(template));
        Assert.Equal("User-edited built-in title", requirements[0].Title);
        Assert.Equal("My template", display.Template("custom", "My template"));
        Assert.Equal("My group", display.Group(new RequirementGroup { Id = "custom", Title = "My group" }));
        Assert.Equal("My requirement", display.Requirement(new ProjectRequirement { DefinitionId = "custom", Title = "My requirement" }));
    }

    [Fact]
    public void Dates_display_local_Gregorian_time_and_edit_invariantly_in_both_languages()
    {
        var locale = new LocaleContext();
        var formatter = new LocaleDateFormatter(locale);
        var instant = new DateTimeOffset(2030, 1, 15, 9, 30, 0, TimeSpan.FromHours(3.5));
        var original = instant;
        var expectedEdit = instant.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        foreach (var language in new[] { "en", "fa" })
        {
            locale.SetLanguage(language);
            Assert.IsType<GregorianCalendar>(locale.Culture.DateTimeFormat.Calendar);
            Assert.Equal(instant.ToLocalTime().ToString("g", locale.Culture), formatter.Display(instant));
            Assert.Contains("2030", formatter.Display(instant));
            Assert.Equal(expectedEdit, formatter.Edit(instant));
            Assert.Equal("", formatter.Display(null));
            Assert.Equal("", formatter.Edit(null));
            Assert.Equal(original, instant);
        }
    }
}
