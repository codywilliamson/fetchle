namespace Fetchle.Core.Search;

// a bad root fails before any walking and is never globbed into a search of its parent,
// see docs/spikes/walker.md
public sealed class InvalidRootException(string path, string reason) : Exception($"{reason}: {path}")
{
    public string Path { get; } = path;

    public static InvalidRootException NotFound(string path) => new(path, "root not found");

    public static InvalidRootException BadSyntax(string path) => new(path, "invalid root path");
}
