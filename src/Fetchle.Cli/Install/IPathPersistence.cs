using Fetchle.Cli.Commands;

namespace Fetchle.Cli.Install;

// where the PATH entry lives between shells: the user registry on Windows, an rc file block on Unix
internal interface IPathPersistence
{
    // how many times the entry is configured. 1 is healthy
    int Count(string entry);

    // idempotent: adds the entry, or collapses duplicates down to one
    PathAdd Add(string entry);

    void Remove(string entry);
}

// what Add did. ReplacedBlock is true only when an existing rc block was rewritten in place, the one case where
// undoing the add means putting the old block back rather than removing ours
internal readonly record struct PathAdd(PathEdit Edit, bool ReplacedBlock);

internal static class PathPersistence
{
    public static IPathPersistence ForSetup(CliEnvironment env)
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsUserPath();
        }

        return new ShellRcBlock(ShellProfile.RcFile(Home(env), env.GetVariable("SHELL")));
    }

    // null when the receipt names a mechanism this OS doesn't have
    public static IPathPersistence? FromReceipt(PathEdit edit)
    {
        if (edit.Kind == PathEditKinds.WINDOWS_USER_PATH && OperatingSystem.IsWindows())
        {
            return new WindowsUserPath();
        }

        if (edit.Kind == PathEditKinds.SHELL_RC_BLOCK && edit.File is not null)
        {
            return new ShellRcBlock(edit.File);
        }

        return null;
    }

    public static string Home(CliEnvironment env)
    {
        var home = env.GetVariable("HOME");
        return string.IsNullOrEmpty(home) ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) : home;
    }
}
