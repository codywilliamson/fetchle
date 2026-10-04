namespace Fetchle.Core;

// linux is case-sensitive, windows and default macos are not
public static class PathComparison
{
    public static readonly StringComparison Default = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
}
