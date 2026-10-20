namespace Fetchle.Cli.Doctor;

internal static class PathCheck
{
    const string NAME = "path";

    // persisted is the registry or rc file, the source of truth. inProcess is this shell's PATH, which lags until a new shell
    public static CheckResult Evaluate(int persisted, int inProcess, string entry)
    {
        if (persisted == 0)
        {
            return new CheckResult(NAME, CheckStatus.Fail, $"{entry} is not on PATH", FixCommands.SETUP);
        }

        if (persisted > 1)
        {
            return new CheckResult(NAME, CheckStatus.Fail, $"{entry} is on PATH {persisted} times", FixCommands.SETUP);
        }

        if (inProcess == 0)
        {
            return new CheckResult(NAME, CheckStatus.Warn, "on PATH, but not in this shell yet. open a new shell");
        }

        if (inProcess > 1)
        {
            return new CheckResult(NAME, CheckStatus.Warn, $"on PATH once, but this shell has it {inProcess} times. open a new shell");
        }

        return new CheckResult(NAME, CheckStatus.Pass, $"{entry} is on PATH once");
    }
}
