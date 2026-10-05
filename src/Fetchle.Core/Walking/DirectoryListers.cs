namespace Fetchle.Core.Walking;

enum ListerKind
{
    Auto,
    DotNet,
}

// picks the lister for a walk. the one place a per-os lister gets added
static class DirectoryListers
{
    public static IDirectoryLister Create(ListerKind kind)
    {
        if (kind == ListerKind.Auto)
        {
            // no native listers yet, every os gets the .NET one
            kind = ListerKind.DotNet;
        }

        return new DotNetDirectoryLister();
    }
}
