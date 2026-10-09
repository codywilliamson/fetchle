using System.IO.Enumeration;

namespace Fetchle.Core.Walking;

// the always-correct lister: FileSystemEnumerator, tasks carry no parent context
sealed class DotNetDirectoryLister : IDirectoryLister
{
    public bool List(in DirTask dir, WalkWorker owner)
    {
        // the include check refuses every entry once the walk is stopping, so this only drains the rest of the dir
        using var enumerator = new Enumerator(dir.Path, owner);
        while (enumerator.MoveNext())
        {
        }

        return !owner.ShouldStop();
    }

    public void Discard(in DirTask dir)
    {
    }

    public void Dispose()
    {
    }

    // overrides instead of delegates, so no delegate or enumerable wrapper per dir
    sealed class Enumerator(string dir, WalkWorker owner) : FileSystemEnumerator<byte>(dir, Options)
    {
        static readonly EnumerationOptions Options = new()
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        protected override bool ShouldIncludeEntry(ref FileSystemEntry entry)
        {
            // budgets are a hard cap, so nothing gets pushed or visited once the walk is stopping
            if (owner.ShouldStop())
            {
                return false;
            }

            var walkEntry = new WalkEntry(entry.Directory, entry.FileName, entry.IsDirectory);
            if (walkEntry.IsDirectory)
            {
                if (owner.Prune.ShouldPrune(walkEntry.Directory, walkEntry.FileName))
                {
                    return false;
                }

                owner.Push(new DirTask(walkEntry.ToFullPath(), null));
            }

            owner.Visit(ref walkEntry);
            return true;
        }

        protected override byte TransformEntry(ref FileSystemEntry entry) => 0;
    }
}
