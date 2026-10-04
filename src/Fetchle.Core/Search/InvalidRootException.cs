namespace Fetchle.Core.Search;

public sealed class InvalidRootException(string path, string reason) : Exception($"{reason}: {path}")
{
    public string Path { get; } = path;

    public static InvalidRootException NotFound(string path) => new(path, "root not found");

    public static InvalidRootException BadSyntax(string path) => new(path, "invalid root path");
}
