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
            // native didn't beat the .NET lister by 15% in the same bench run (see the commit that added it), so Auto stays on .NET
            kind = ListerKind.DotNet;
        }

        // an explicit Native on a machine that can't run it still lists, just with the .NET lister
        if (kind == ListerKind.Native && OperatingSystem.IsWindows() && NativeSupport.IsAvailable)
        {
            return new WindowsDirectoryLister();
        }

        return new DotNetDirectoryLister();
    }
}
