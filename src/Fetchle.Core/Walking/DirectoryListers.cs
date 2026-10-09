using Fetchle.Core.Walking.Windows;

namespace Fetchle.Core.Walking;

enum ListerKind
{
    Auto,
    DotNet,
    Native,
}

// picks the lister for a walk. the one place a per-os lister gets added
static class DirectoryListers
{
    public static IDirectoryLister Create(ListerKind kind)
    {
        if (kind == ListerKind.Auto)
        {
            // native ties .NET on speed but opens every child relative to its parent with links refused,
            // so a dir swapped for a junction mid-walk can't redirect it. see docs/decisions.md
            kind = OperatingSystem.IsWindows() ? ListerKind.Native : ListerKind.DotNet;
        }

        // an explicit Native on a machine that can't run it still lists, just with the .NET lister
        if (kind == ListerKind.Native && OperatingSystem.IsWindows() && NativeSupport.IsAvailable)
        {
            return new WindowsDirectoryLister();
        }

        return new DotNetDirectoryLister();
    }
}
