using Fetchle.Cli.Commands;

namespace Fetchle.Cli.Install;

// v0.1 setup: put the install dir on PATH and write the receipt. agent registration arrives in v0.3, so agents stay empty
internal static class SetupAction
{
    public static Receipt Run(CliEnvironment env, TextWriter output)
    {
        var dirs = env.Dirs;
        var entry = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dirs.Bin));
        var previous = LoadPrevious(dirs.Receipt);

        var persistence = PathPersistence.ForSetup(env);
        var alreadyOnPath = persistence.Count(entry) > 0;
        var edit = persistence.Add(entry);

        var receipt = new Receipt(env.Version, Channels.SCRIPT, entry, Path.GetFullPath(dirs.Data), [], edit);
        try
        {
            ReceiptStore.Save(dirs.Receipt, receipt);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // no receipt means uninstall can't find the PATH edit later, so don't leave one it can't undo
            if (!alreadyOnPath)
            {
                persistence.Remove(entry);
            }
            throw;
        }

        // only once the new receipt is safe: the old edit is dropped when nothing records it anymore
        RemoveStaleEdit(previous?.PathEdit, edit);

        output.WriteLine(edit.File is null ? $"on your user PATH: {entry}" : $"on PATH via {edit.File}: {entry}");
        output.WriteLine($"receipt: {dirs.Receipt}");
        output.WriteLine("open a new shell for PATH to take effect. agent registration arrives in v0.3");
        WarnWhenExeIsElsewhere(entry, output);
        return receipt;
    }

    // a corrupt receipt gets overwritten, it only ever records what this run does
    static Receipt? LoadPrevious(string path)
    {
        try
        {
            return ReceiptStore.Load(path);
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    // the shell, OS flavor or install dir changed since last time, so the old edit would be orphaned. an rc block
    // in the same file was already replaced in place by Add, so removing it again would undo the new one
    static void RemoveStaleEdit(PathEdit? old, PathEdit current)
    {
        if (old is null || (old.Kind == current.Kind && old.File == current.File
            && (old.Kind == PathEditKinds.SHELL_RC_BLOCK || PathRules.Current.Key(old.Entry).Equals(PathRules.Current.Key(current.Entry)))))
        {
            return;
        }

        PathPersistence.FromReceipt(old)?.Remove(old.Entry);
    }

    static void WarnWhenExeIsElsewhere(string entry, TextWriter output)
    {
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath);
        if (exeDir is not null && !PathRules.Current.Key(exeDir).Equals(PathRules.Current.Key(entry)))
        {
            output.WriteLine($"note: this fetchle runs from {exeDir}, not the install dir, so the PATH entry may not contain it");
        }
    }
}
