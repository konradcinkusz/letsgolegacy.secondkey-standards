namespace SecondKey.Standards.Generator.Tests.Support;

/// <summary>A throwaway consuming repository: files are written relative to its root.</summary>
internal sealed class Consumer : IDisposable
{
    public Consumer()
    {
        Root = Directory.CreateTempSubdirectory("secondkey-consumer-").FullName;
    }

    public string Root { get; }

    public Consumer With(string relativePath, string contents)
    {
        var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        return this;
    }

    public Consumer WithCentralPin(string version) =>
        With("Directory.Packages.props", $"""
            <Project>
              <PropertyGroup>
                <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
              </PropertyGroup>
              <ItemGroup>
                <GlobalPackageReference Include="SecondKey.Standards" Version="{version}" />
              </ItemGroup>
            </Project>
            """);

    public Consumer WithSkill(string version, string root = ".github/skills") =>
        With($"{root}/applying-dotnet-migration-standards/SKILL.md", $"""
            ---
            name: applying-dotnet-migration-standards
            description: "Applies the Second Key migration standards."
            metadata:
              discovery: "preload"
              version: "{version}"
            ---

            # Skill
            """);

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
