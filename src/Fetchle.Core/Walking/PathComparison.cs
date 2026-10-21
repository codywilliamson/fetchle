using System.Collections.Concurrent;

namespace Fetchle.Core.Walking;

// linux is case-sensitive, windows and default macos are not. macos can also have case-sensitive volumes, so For
// asks the volume there
public static class PathComparison
{
    public static readonly StringComparison Default = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    public static readonly StringComparer Comparer = OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    static readonly ConcurrentDictionary<string, StringComparison> MacVolumes = new(StringComparer.Ordinal);

    // how paths under this one compare. the default everywhere except macos, where the volume decides (cached per path)
    public static StringComparison For(string path) =>
        OperatingSystem.IsMacOS() ? MacVolumes.GetOrAdd(path, p => ForVolume(p, MacVolume.CaseSensitive)) : Default;

    // asks the volume, never a sibling path: a case-sensitive volume can hold both Foo and FoO, so whether a flipped
    // spelling exists says nothing. caseSensitive is pathconf(_PC_CASE_SENSITIVE): 1 sensitive, 0 not, -1 unknown,
    // and unknown keeps the case-insensitive default macos ships with
    public static StringComparison ForVolume(string path, Func<string, long> caseSensitive) =>
        caseSensitive(path) == 1 ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
}
