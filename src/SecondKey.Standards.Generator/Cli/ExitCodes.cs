namespace SecondKey.Standards.Generator.Cli;

/// <summary>
/// Exit codes, the same for every command, so a pipeline can tell "the check ran and failed"
/// from "the check could not run". Both fail a CI step; only the second is worth retrying.
/// </summary>
public static class ExitCodes
{
    /// <summary>The command ran and everything it checked is in order.</summary>
    public const int Success = 0;

    /// <summary>The command ran and found a problem: invalid standards, a stale tree, or drift.</summary>
    public const int CheckFailed = 1;

    /// <summary>The command could not run: bad arguments, a missing file, an unreachable source.</summary>
    public const int CouldNotRun = 2;
}
