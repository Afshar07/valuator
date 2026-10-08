using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Domain;

namespace ProjectOperations.Desktop.Localization;

public interface IDomainDisplay
{
    string Get(Enum value);
    string Enum<T>(T value) where T : struct, Enum;
    string Template(ProjectTemplate template);
    string Template(string id, string original);
    string Group(RequirementGroup group);
    string Group(string id, string original);
    string Requirement(ProjectRequirement requirement);
    string Requirement(string id, string original);
}

public sealed class DomainDisplay(ILocalizationService localization) : IDomainDisplay
{
    private static readonly ProjectTemplate BuiltIn = VcTemplate.Create();

    public string Enum<T>(T value) where T : struct, Enum => Get(value);

    public string Get(Enum value)
    {
        var prefix = value switch
        {
            ProjectStatus => "project.status",
            RequirementType => "requirement.type",
            RequirementStatus => "requirement.status",
            ProjectTaskStatus => "task.status",
            AgentJobStatus => "agent.status",
            ProposalReviewStatus => "proposal.status",
            _ => null
        };
        var name = value.ToString();
        return prefix is null ? name : localization.Get($"{prefix}.{char.ToLowerInvariant(name[0])}{name[1..]}");
    }

    public string Template(ProjectTemplate template) => Template(template.Id, template.Name);
    public string Template(string id, string original) => id == BuiltIn.Id ? localization.Get($"template.{id}") : original;
    public string Group(RequirementGroup group) => Group(group.Id, group.Title);
    public string Group(string id, string original) => BuiltIn.Groups.Any(group => group.Id == id)
        ? localization.Get($"template.group.{id}") : original;
    public string Requirement(ProjectRequirement requirement) => Requirement(requirement.DefinitionId, requirement.Title);
    public string Requirement(string id, string original) => BuiltIn.Groups.SelectMany(group => group.Requirements).Any(requirement => requirement.Id == id)
        ? localization.Get($"template.requirement.{id}") : original;
}
