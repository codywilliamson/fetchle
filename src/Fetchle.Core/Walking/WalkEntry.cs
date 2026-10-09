namespace Fetchle.Core.Walking;

// one visited entry, built from spans the lister already has, so nothing allocates until ToFullPath
public readonly ref struct WalkEntry(ReadOnlySpan<char> directory, ReadOnlySpan<char> fileName, bool isDirectory)
{
    // parent full path, no trailing separator except on a root like C:\
    public ReadOnlySpan<char> Directory { get; } = directory;

    public ReadOnlySpan<char> FileName { get; } = fileName;

    public bool IsDirectory { get; } = isDirectory;

    public string ToFullPath() => Path.Join(Directory, FileName);
}
