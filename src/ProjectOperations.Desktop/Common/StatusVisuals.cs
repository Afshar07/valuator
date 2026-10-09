using ProjectOperations.Core.Domain;

namespace ProjectOperations.Desktop.Common;

/// <summary>Icon, weight and colour tone of each domain status, in one place for every view-model that shows one.</summary>
internal static class StatusVisuals
{
    public static (string Icon, IconWeight Weight, Tone Tone) Requirement(RequirementStatus status) => status switch
    {
        RequirementStatus.Complete => (Icons.CheckCircle, IconWeight.Fill, Tone.Success),
        RequirementStatus.Provided => (Icons.CircleHalf, IconWeight.Regular, Tone.Accent),
        RequirementStatus.NeedsReview => (Icons.WarningCircle, IconWeight.Regular, Tone.Warning),
        _ => (Icons.CircleDashed, IconWeight.Regular, Tone.Error)
    };

    public static Tone Project(ProjectStatus status) => status switch
    {
        ProjectStatus.Active => Tone.Success,
        ProjectStatus.OnHold => Tone.Neutral,
        ProjectStatus.Completed => Tone.Accent,
        _ => Tone.Neutral
    };

    public static (string Icon, IconWeight Weight, string Color) Task(ProjectTaskStatus status) => status switch
    {
        ProjectTaskStatus.Done => (Icons.CheckCircle, IconWeight.Fill, "Success"),
        ProjectTaskStatus.Cancelled => (Icons.Prohibit, IconWeight.Regular, "TextTertiary"),
        ProjectTaskStatus.InProgress => (Icons.CircleHalf, IconWeight.Regular, "Accent"),
        _ => (Icons.Circle, IconWeight.Regular, "TextTertiary")
    };
}
