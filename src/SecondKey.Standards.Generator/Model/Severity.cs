namespace SecondKey.Standards.Generator.Model;

/// <summary>
/// How much a rule matters, in one vocabulary shared by the agent and the gate. The names are
/// the <c>.editorconfig</c> severities they compile to, so a rule means the same thing in the
/// skill that told the agent and in the diagnostic that checks the result.
/// </summary>
public enum Severity
{
    /// <summary>Migrated code must not violate the rule; the gate fails the change.</summary>
    Error,

    /// <summary>Violations are reported; any that remain are listed as behaviour flags.</summary>
    Warning,

    /// <summary>Applied where the change is mechanical and behaviour-neutral; never blocking.</summary>
    Suggestion,
}

public static class SeverityNames
{
    public static IReadOnlyList<string> All { get; } = ["error", "warning", "suggestion"];

    public static bool TryParse(string value, out Severity severity)
    {
        switch (value)
        {
            case "error":
                severity = Severity.Error;
                return true;
            case "warning":
                severity = Severity.Warning;
                return true;
            case "suggestion":
                severity = Severity.Suggestion;
                return true;
            default:
                severity = default;
                return false;
        }
    }

    public static string ToName(this Severity severity) => severity switch
    {
        Severity.Error => "error",
        Severity.Warning => "warning",
        Severity.Suggestion => "suggestion",
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null),
    };
}
