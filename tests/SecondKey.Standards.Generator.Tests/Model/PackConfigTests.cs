using SecondKey.Standards.Generator.Model;
using SecondKey.Standards.Generator.Tests.Support;
using Xunit;

namespace SecondKey.Standards.Generator.Tests.Model;

public class PackConfigTests
{
    private static string LoadError(Action<Dictionary<string, object>> change)
    {
        using var repository = new TestRepository();
        var pack = (Dictionary<string, object>)TestRepository.Pack();
        change(pack);
        repository.WritePack(pack);

        return Assert.Throws<PackConfigException>(() => PackConfig.Load(repository.Layout.PackConfigPath)).Message;
    }

    [Fact]
    public void The_repository_pack_is_valid()
    {
        var pack = PackConfig.Load(RepositoryRoot.Layout.PackConfigPath);

        Assert.False(pack.ParsedVersion.IsPrerelease);
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("1.0.0-beta")]
    public void The_version_must_be_a_release_version(string version)
    {
        Assert.Contains("\"version\"", LoadError(pack => pack["version"] = version), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Applying-Standards", "lower-case letters")]
    [InlineData("applying--standards", "lower-case letters")]
    [InlineData("claude-standards", "reserved word")]
    public void The_skill_name_follows_the_agent_skills_specification(string name, string expected)
    {
        var message = LoadError(pack => pack["skill"] = new { name, description = "Applies standards.", discovery = "preload", traits = ".NET" });

        Assert.Contains(expected, message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_skill_description_is_at_most_1024_characters()
    {
        var message = LoadError(pack => pack["skill"] = new { name = "applying-standards", description = new string('x', 1025), discovery = "lazy", traits = ".NET" });

        Assert.Contains("1 to 1024 characters", message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_skill_description_has_no_angle_brackets()
    {
        var message = LoadError(pack => pack["skill"] = new { name = "applying-standards", description = "Uses IOptions<T>.", discovery = "lazy", traits = ".NET" });

        Assert.Contains("must not contain < or >", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Discovery_is_preload_or_lazy()
    {
        var message = LoadError(pack => pack["skill"] = new { name = "applying-standards", description = "Applies.", discovery = "scenario", traits = ".NET" });

        Assert.Contains("\"skill.discovery\"", message, StringComparison.Ordinal);
    }
}
