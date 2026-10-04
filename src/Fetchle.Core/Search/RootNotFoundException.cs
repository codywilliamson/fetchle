namespace Fetchle.Core.Search;

// a missing root is never globbed into a search of its parent, see docs/spikes/walker.md
public sealed class RootNotFoundException(string path) : Exception($"root not found: {path}")
{
    public string Path { get; } = path;
}
