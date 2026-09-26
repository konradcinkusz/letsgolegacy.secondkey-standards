namespace SecondKey.Standards.Generator.Tests.Support;

/// <summary>Finds this repository's root from the test binaries, so tests can check the real standards.</summary>
internal static class RepositoryRoot
{
    public static RepositoryLayout Layout { get; } = Find();

    private static RepositoryLayout Find() =>
        RepositoryLayout.Discover(explicitRoot: null, AppContext.BaseDirectory)
        ?? throw new InvalidOperationException(
            $"No {RepositoryLayout.PackConfigRelativePath} above {AppContext.BaseDirectory}; run the tests from a clone of the repository.");
}
