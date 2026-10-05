namespace Fetchle.Core.Walking;

// one dir waiting to be listed. Parent is whatever the lister wants to hand its children (null for the .NET lister).
// whoever holds an unlisted task must give it back to IDirectoryLister.Discard
readonly struct DirTask(string path, object? parent)
{
    public string Path { get; } = path;

    public object? Parent { get; } = parent;
}
