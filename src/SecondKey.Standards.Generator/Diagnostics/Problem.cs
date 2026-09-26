using System.Globalization;

namespace SecondKey.Standards.Generator.Diagnostics;

/// <summary>
/// A defect in the standards sources, located as precisely as the parser can: always the
/// repository-relative file, and the line when one is known. Problems are collected rather
/// than thrown, so one run reports every broken rule instead of the first.
/// </summary>
public sealed record Problem(string Path, int? Line, string Message)
{
    public override string ToString() =>
        Line is { } line
            ? string.Create(CultureInfo.InvariantCulture, $"{Path}:{line}: {Message}")
            : $"{Path}: {Message}";
}
