namespace Fetchle.Core.Embedding;

// the model file exists but can't be trusted: truncated, corrupt, wrong version or wrong endianness
public sealed class ModelUnreadableException(string path, string reason) : Exception($"model unreadable ({reason}): {path}")
{
    public string Path { get; } = path;

    public string Reason { get; } = reason;
}
