namespace SecondKey.Standards.Generator.Generation;

/// <summary>Where each generated artifact lives, relative to the repository root.</summary>
public static class GeneratedPaths
{
    public const string Manifest = "generated/manifest.json";
    public const string EditorConfig = "generated/config/.editorconfig";
    public const string GlobalConfig = "generated/config/.globalconfig";
    public const string PackageDirectory = "generated/nuget";

    public static string SkillDirectory(string skillName) => $"generated/skills/{skillName}";

    public static string SkillFile(string skillName) => $"{SkillDirectory(skillName)}/SKILL.md";

    public static string Reference(string skillName, string ruleId) => $"{SkillDirectory(skillName)}/references/{ruleId}.md";

    public static string PackageProject(string packageId) => $"{PackageDirectory}/{packageId}.csproj";

    public static string PackageProps(string packageId) => $"{PackageDirectory}/build/{packageId}.props";

    public static string PackageReadme => $"{PackageDirectory}/README.md";
}
