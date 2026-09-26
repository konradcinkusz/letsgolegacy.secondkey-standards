using SecondKey.Standards.Generator.Drift;
using SecondKey.Standards.Generator.Tests.Support;
using Xunit;

namespace SecondKey.Standards.Generator.Tests.Drift;

public class PinReaderTests
{
    private static PinScan Scan(Consumer consumer) =>
        PinReader.Scan(consumer.Root, DriftDefaults.PackageId, DriftDefaults.SkillName);

    private static void AssertSinglePin(PinScan scan, PinKind kind, string location, string version)
    {
        Assert.Empty(scan.Problems);
        var pin = Assert.Single(scan.Pins);
        Assert.Equal((kind, location, version), (pin.Kind, pin.Location, pin.Version.ToString()));
    }

    [Fact]
    public void A_central_package_version_is_a_pin()
    {
        using var consumer = new Consumer().With("Directory.Packages.props", """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
                <PackageVersion Include="SecondKey.Standards" Version="0.3.0" />
              </ItemGroup>
            </Project>
            """);

        AssertSinglePin(Scan(consumer), PinKind.Package, "Directory.Packages.props", "0.3.0");
    }

    [Fact]
    public void A_global_package_reference_is_a_pin()
    {
        using var consumer = new Consumer().WithCentralPin("1.2.3");

        AssertSinglePin(Scan(consumer), PinKind.Package, "Directory.Packages.props", "1.2.3");
    }

    [Fact]
    public void A_project_package_reference_is_a_pin_whatever_the_casing_or_the_version_notation()
    {
        using var consumer = new Consumer().With("src/Shop/Shop.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="secondkey.standards" Version="[1.0.0]" PrivateAssets="all" />
              </ItemGroup>
            </Project>
            """);

        AssertSinglePin(Scan(consumer), PinKind.Package, "src/Shop/Shop.csproj", "1.0.0");
    }

    [Fact]
    public void A_version_in_a_child_element_of_an_old_style_project_is_a_pin()
    {
        using var consumer = new Consumer().With("Shop.Web/Shop.Web.csproj", """
            <?xml version="1.0" encoding="utf-8"?>
            <Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <ItemGroup>
                <PackageReference Include="SecondKey.Standards">
                  <Version>0.2.0</Version>
                </PackageReference>
              </ItemGroup>
            </Project>
            """);

        AssertSinglePin(Scan(consumer), PinKind.Package, "Shop.Web/Shop.Web.csproj", "0.2.0");
    }

    [Fact]
    public void A_packages_config_entry_is_a_pin()
    {
        using var consumer = new Consumer().With("Shop.Web/packages.config", """
            <?xml version="1.0" encoding="utf-8"?>
            <packages>
              <package id="SecondKey.Standards" version="0.1.0" developmentDependency="true" targetFramework="net48" />
            </packages>
            """);

        AssertSinglePin(Scan(consumer), PinKind.Package, "Shop.Web/packages.config", "0.1.0");
    }

    [Theory]
    [InlineData(".github/skills")]
    [InlineData(".github/upgrades/skills")]
    [InlineData(".claude/skills")]
    public void An_installed_skill_is_a_pin(string root)
    {
        using var consumer = new Consumer().WithSkill("0.4.0", root);

        AssertSinglePin(Scan(consumer), PinKind.Skill, $"{root}/applying-dotnet-migration-standards/SKILL.md", "0.4.0");
    }

    [Fact]
    public void A_skill_without_a_version_is_a_problem()
    {
        using var consumer = new Consumer().With(".github/skills/applying-dotnet-migration-standards/SKILL.md", "---\nname: applying-dotnet-migration-standards\ndescription: x\n---\n");

        var scan = Scan(consumer);

        Assert.Empty(scan.Pins);
        Assert.Contains("has no readable metadata.version", Assert.Single(scan.Problems), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("$(StandardsVersion)", "MSBuild property")]
    [InlineData("0.*", "not an exact")]
    [InlineData("[1.0.0,2.0.0)", "not an exact")]
    public void A_version_that_is_not_an_exact_pin_is_a_problem(string version, string expected)
    {
        using var consumer = new Consumer().WithCentralPin(version);

        var scan = Scan(consumer);

        Assert.Contains(expected, Assert.Single(scan.Problems), StringComparison.Ordinal);
    }

    [Fact]
    public void Build_output_and_unrelated_files_are_ignored()
    {
        using var consumer = new Consumer()
            .With("src/Shop/obj/project.assets.props", """<Project><ItemGroup><PackageReference Include="SecondKey.Standards" Version="9.9.9" /></ItemGroup></Project>""")
            .With("broken.props", "<Project><not closed")
            .With("README.md", "SecondKey.Standards 9.9.9");

        var scan = Scan(consumer);

        Assert.Empty(scan.Pins);
        Assert.Empty(scan.Problems);
    }

    [Fact]
    public void Every_pin_is_reported_so_disagreements_can_be_seen()
    {
        using var consumer = new Consumer().WithCentralPin("0.2.0").WithSkill("0.1.0");

        var scan = Scan(consumer);

        Assert.Equal(["0.2.0", "0.1.0"], scan.Pins.Select(pin => pin.Version.ToString()));
    }
}
