using ProjectOperations.Core.Agents;
using Xunit;

namespace ProjectOperations.Tests;

public sealed class AgentPromptsTests
{
    [Theory]
    [InlineData(AgentResponseLanguage.English, "English")]
    [InlineData(AgentResponseLanguage.Persian, "Persian")]
    public void LanguageInstructionsPreserveTheSchemaAndSafetyInstructions(AgentResponseLanguage language, string name)
    {
        var instructions = AgentPrompts.BuildOutputInstructions(language);
        Assert.StartsWith(AgentPrompts.OutputInstructions + "\n\n", instructions);
        Assert.Contains($"Write all response prose in {name}, including task title and description values.", instructions);
        Assert.Contains("Do not translate JSON keys (tasks, title, description, dueAt), the task-proposals fence tag, or dueAt values.", instructions);
        Assert.Contains("{\"tasks\":[{\"title\":\"Request updated forecast\",\"description\":\"Why this is needed\",\"dueAt\":null}]}", instructions);
        Assert.Contains("Preserve null and ISO 8601 timestamps with a timezone", instructions);
        Assert.Contains("Keep file paths, IDs, company names and technical identifiers unchanged.", instructions);
    }

    [Fact]
    public void PredefinedActionsHaveStableUniqueIdsIndependentOfTheirNames()
    {
        Assert.Equal(new[] { "summarizeProject", "findMissingInformation", "compareProjectInformation",
            "extractActionItems", "prepareMeetingBrief", "needsAttention" }, AgentPrompts.Actions.Select(action => action.Id));
        Assert.Equal(AgentPrompts.Actions.Count, AgentPrompts.Actions.Select(action => action.Id).Distinct().Count());
        var action = AgentPrompts.Actions[0];
        Assert.Equal(action.Id, (action with { Name = "خلاصه پروژه" }).Id);
    }
}
