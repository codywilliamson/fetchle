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
        OperatingSystem.IsMacOS() ? MacVolumes.GetOrAdd(path, p => ForVolume(p, Directory.Exists)) : Default;

    // a case-sensitive volume has no directory under the same path with its case flipped. a path that isn't there
    // or has no letters to flip tells us nothing, so it keeps the case-insensitive default
    public static StringComparison ForVolume(string path, Func<string, bool> directoryExists)
    {
        var letter = path.Length - 1;
        while (letter >= 0 && !char.IsAsciiLetter(path[letter]))
        {
            letter--;
        }

        if (letter < 0 || !directoryExists(path))
        {
            return StringComparison.OrdinalIgnoreCase;
        }

        var flipped = string.Create(path.Length, (path, letter), static (span, state) =>
        {
            state.path.CopyTo(span);
            span[state.letter] = (char)(state.path[state.letter] ^ 0x20);
        });
        return directoryExists(flipped) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    }
}
