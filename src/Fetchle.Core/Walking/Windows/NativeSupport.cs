using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Fetchle.Core.Walking.Windows;

// whether this machine can run the native lister, probed once per process
static class NativeSupport
{
    public static bool IsAvailable { get; } = OperatingSystem.IsWindows() && Probe();

    [SupportedOSPlatform("windows")]
    static bool Probe()
    {
        // NtQueryDirectoryFileEx needs windows 10 1709
        if (!NativeLibrary.TryLoad("ntdll.dll", out var ntdll) || !NativeLibrary.TryGetExport(ntdll, "NtQueryDirectoryFileEx", out _))
        {
            return false;
        }

        // an old kernel rejects OBJ_DONT_REPARSE with an invalid parameter, so try one relative open
        var system = Environment.SystemDirectory;
        var parentPath = Path.GetDirectoryName(system);
        if (parentPath is null)
        {
            return false;
        }

        if (NtApi.OpenDirectory(0, WindowsDirectoryLister.ToNtPath(parentPath), 0, WindowsDirectoryLister.ROOT_OPTIONS, out var parent) < 0)
        {
            return false;
        }

        try
        {
            if (NtApi.OpenDirectory(parent, Path.GetFileName(system), NtApi.OBJ_DONT_REPARSE, WindowsDirectoryLister.CHILD_OPTIONS, out var child) < 0)
            {
                return false;
            }

            NtApi.Close(child);
            return true;
        }
        finally
        {
            NtApi.Close(parent);
        }
    }
}
