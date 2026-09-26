using SecondKey.Standards.Generator.Model;
using Xunit;

namespace SecondKey.Standards.Generator.Tests.Model;

public class SemanticVersionTests
{
    [Theory]
    [InlineData("0.1.0", "0.2.0")]
    [InlineData("0.9.0", "0.10.0")]
    [InlineData("1.0.0-rc.1", "1.0.0")]
    [InlineData("1.0.0", "1.0.1")]
    [InlineData("1.9.9", "2.0.0")]
    public void Versions_order_numerically_and_prereleases_first(string lower, string higher)
    {
        Assert.True(SemanticVersion.Parse(lower) < SemanticVersion.Parse(higher));
        Assert.True(SemanticVersion.Parse(higher) > SemanticVersion.Parse(lower));
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("v1.0.0")]
    [InlineData("01.0.0")]
    [InlineData("1.0.0.0")]
    [InlineData("")]
    public void Only_three_part_versions_parse(string text)
    {
        Assert.False(SemanticVersion.TryParse(text, out _));
    }

    [Fact]
    public void Build_metadata_is_not_part_of_the_version()
    {
        Assert.Equal(0, SemanticVersion.Parse("1.2.3+build.7").CompareTo(SemanticVersion.Parse("1.2.3")));
        Assert.Equal("1.2.3-rc.1", SemanticVersion.Parse("1.2.3-rc.1+x").ToString());
    }
}
