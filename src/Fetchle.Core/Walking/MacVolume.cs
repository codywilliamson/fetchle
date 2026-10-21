using System.Runtime.InteropServices;

namespace Fetchle.Core.Walking;

// the one question fetchle asks a macos volume: does it fold case
static partial class MacVolume
{
    // <unistd.h> on darwin
    const int PC_CASE_SENSITIVE = 11;

    // 1 case-sensitive, 0 case-insensitive, -1 when the volume won't say (or the path is gone)
    public static long CaseSensitive(string path) => PathConf(path, PC_CASE_SENSITIVE);

    [LibraryImport("libc", EntryPoint = "pathconf", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint PathConf(string path, int name);
}
