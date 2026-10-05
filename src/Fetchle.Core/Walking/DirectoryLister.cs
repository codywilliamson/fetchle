using System.IO.Enumeration;

namespace Fetchle.Core.Walking;

// lists one directory: visits each entry, hands unpruned subdirectories back to its worker.
// overrides instead of delegates, so no delegate or enumerable wrapper per dir
sealed class DirectoryLister(string dir, WalkWorker owner) : FileSystemEnumerator<byte>(dir, Options)
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
        if (entry.IsDirectory)
        {
            if (owner.Prune.ShouldPrune(entry.Directory, entry.FileName))
            {
                return false;
            }

            owner.Push(entry.ToFullPath());
        }

        owner.Visit(ref entry);
        return true;
    }

    protected override byte TransformEntry(ref FileSystemEntry entry) => 0;
}
