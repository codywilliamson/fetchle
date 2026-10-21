using Fetchle.Cli.Commands;

namespace Fetchle.Cli.Install;

// v0.1 setup: put the install dir on PATH and write the receipt. agent registration arrives in v0.3, so agents stay empty
internal static class SetupAction
{
    // persistence is a seam for tests, which must not edit the real user PATH
    public static Receipt Run(CliEnvironment env, TextWriter output, IPathPersistence? persistence = null)
    {
        var dirs = env.Dirs;
        var entry = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dirs.Bin));
        var previous = LoadPrevious(dirs.Receipt);

        persistence ??= PathPersistence.ForSetup(env);
        var alreadyOnPath = persistence.Count(entry) > 0;
        var edit = persistence.Add(entry);
        // a windows PATH entry the user had before fetchle isn't ours to record, so uninstall leaves it alone.
        // a marker block is always ours, and a previous receipt naming this entry means fetchle added it back then
        var owned = !alreadyOnPath || edit.Kind == PathEditKinds.SHELL_RC_BLOCK || SameEntry(previous?.PathEdit, edit);
        var receipt = new Receipt(env.Version, Channels.SCRIPT, entry, Path.GetFullPath(dirs.Data), [], owned ? edit : null);
        var removedStale = false;
        try
        {
            // the old edit goes first, while the old receipt still records it: if removing it fails, nothing has
            // been forgotten yet. only then does the new receipt replace the old one
            removedStale = RemoveStaleEdit(previous?.PathEdit, edit);
            ReceiptStore.Save(dirs.Receipt, receipt);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // put things back the way the old receipt describes them: no new entry, the old edit restored
            // a block in the same rc file was replaced in place, so its old text goes back below instead of a removal
            var replacedInPlace = IsSameRcFile(previous?.PathEdit, edit);
            if (!alreadyOnPath && !replacedInPlace)
            {
                persistence.Remove(entry);
            }

            if ((removedStale || replacedInPlace) && previous?.PathEdit is { } old)
            {
                PathPersistence.FromReceipt(old)?.Add(old.Entry);
            }
            throw;
        }

        output.WriteLine(edit.File is null ? $"on your user PATH: {entry}" : $"on PATH via {edit.File}: {entry}");
        if (!owned)
        {
            output.WriteLine("that PATH entry was already there before fetchle, so uninstall will leave it");
        }
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
    // returns whether it removed anything
    static bool RemoveStaleEdit(PathEdit? old, PathEdit current)
    {
        if (old is null || (old.Kind == current.Kind && old.File == current.File
            && (old.Kind == PathEditKinds.SHELL_RC_BLOCK || SameEntry(old, current))))
        {
            return false;
        }

        var persistence = PathPersistence.FromReceipt(old);
        persistence?.Remove(old.Entry);
        return persistence is not null;
    }

    static bool IsSameRcFile(PathEdit? old, PathEdit current) =>
        old is { Kind: PathEditKinds.SHELL_RC_BLOCK } && old.Kind == current.Kind && old.File == current.File;

    static bool SameEntry(PathEdit? a, PathEdit b) =>
        a is not null && a.Kind == b.Kind && PathRules.Current.Key(a.Entry).Equals(PathRules.Current.Key(b.Entry));

    static void WarnWhenExeIsElsewhere(string entry, TextWriter output)
    {
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath);
        if (exeDir is not null && !PathRules.Current.Key(exeDir).Equals(PathRules.Current.Key(entry)))
        {
            output.WriteLine($"note: this fetchle runs from {exeDir}, not the install dir, so the PATH entry may not contain it");
        }
    }
}
