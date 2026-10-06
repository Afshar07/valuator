using System.Text.Json;
using System.Text.RegularExpressions;

namespace ProjectOperations.Core.Agents;

public static partial class TaskProposalParser
{
    public static List<TaskProposal> Parse(string text)
    {
        var match = ProposalBlock().Match(text);
        if (!match.Success || match.Groups[1].Value.Length > 32_768)
            return [];
        try
        {
            using var document = JsonDocument.Parse(match.Groups[1].Value);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("tasks", out var tasks) || tasks.ValueKind != JsonValueKind.Array)
                return [];
            var result = new List<TaskProposal>();
            foreach (var item in tasks.EnumerateArray().Take(30))
            {
                if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("title", out var title)
                    || title.ValueKind != JsonValueKind.String)
                    continue;
                var name = title.GetString()?.Trim() ?? "";
                if (name.Length is 0 or > 300)
                    continue;
                var description = item.TryGetProperty("description", out var desc) && desc.ValueKind == JsonValueKind.String
                    ? desc.GetString() ?? "" : "";
                if (description.Length > 4000)
                    continue;
                DateTimeOffset? dueAt = null;
                if (item.TryGetProperty("dueAt", out var due) && due.ValueKind == JsonValueKind.String)
                {
                    var value = due.GetString() ?? "";
                    if ((value.EndsWith('Z') || OffsetSuffix().IsMatch(value)) && due.TryGetDateTimeOffset(out var date))
                        dueAt = date;
                }
                result.Add(new TaskProposal { Title = name, Description = description, DueAt = dueAt });
            }
            return result;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    [GeneratedRegex(@"```task-proposals\s*\n([\s\S]*?)```", RegexOptions.CultureInvariant)]
    private static partial Regex ProposalBlock();

    [GeneratedRegex(@"[+-]\d{2}:\d{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex OffsetSuffix();
}
