using SecondKey.Standards.Generator.Generation;

namespace SecondKey.Standards.Generator.Drift;

public enum RuleChangeKind
{
    Added,
    Removed,
    Changed,
}

/// <summary>One rule's difference between two released versions.</summary>
public sealed record RuleChange(RuleChangeKind Kind, string Id, string Title, string Detail);

/// <summary>
/// What changed between two versions' manifests, rule by rule: the mechanical half of "what
/// changed" (the changelog is the human half). A severity change is spelled out, because it is the
/// change that decides whether upgrading costs work.
/// </summary>
public static class ManifestDiff
{
    public static IReadOnlyList<RuleChange> Compare(Manifest pinned, Manifest latest)
    {
        var before = pinned.Rules.ToDictionary(rule => rule.Id, StringComparer.Ordinal);
        var after = latest.Rules.ToDictionary(rule => rule.Id, StringComparer.Ordinal);
        var changes = new List<RuleChange>();

        foreach (var rule in latest.Rules)
        {
            if (!before.TryGetValue(rule.Id, out var old))
            {
                changes.Add(new RuleChange(RuleChangeKind.Added, rule.Id, rule.Title, rule.Severity));
                continue;
            }

            var details = new List<string>();
            if (!string.Equals(old.Severity, rule.Severity, StringComparison.Ordinal))
            {
                details.Add($"severity {old.Severity} → {rule.Severity}");
            }

            if (!old.Diagnostics.SequenceEqual(rule.Diagnostics, StringComparer.Ordinal))
            {
                details.Add($"diagnostics {Join(old.Diagnostics)} → {Join(rule.Diagnostics)}");
            }

            if (details.Count == 0 && !string.Equals(old.Hash, rule.Hash, StringComparison.Ordinal))
            {
                details.Add("text");
            }

            if (details.Count > 0)
            {
                changes.Add(new RuleChange(RuleChangeKind.Changed, rule.Id, rule.Title, string.Join("; ", details)));
            }
        }

        foreach (var rule in pinned.Rules.Where(rule => !after.ContainsKey(rule.Id)))
        {
            changes.Add(new RuleChange(RuleChangeKind.Removed, rule.Id, rule.Title, rule.Severity));
        }

        return changes;
    }

    private static string Join(IReadOnlyList<string> diagnostics) =>
        diagnostics.Count == 0 ? "(none)" : string.Join(", ", diagnostics);
}
