namespace Fetchle.Cli.Commands;

// the parser reports bad args as 1, our "no results". an action that ran marks itself so its own exit code stands
sealed class ActionTracker
{
    public bool Ran { get; private set; }

    public ValueTask<int> Done(ExitCode exitCode)
    {
        Ran = true;
        return ValueTask.FromResult((int)exitCode);
    }
}
