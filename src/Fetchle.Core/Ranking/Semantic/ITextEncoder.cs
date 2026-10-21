namespace Fetchle.Core.Ranking.Semantic;

// what the semantic retriever needs from an embedding model. Embedding.StaticModel is the real one; tests use a fake
public interface ITextEncoder
{
    // identifies the model a vector store was built with
    ulong Checksum { get; }

    // the mean of the model's vectors, subtracted before taking signs so the bits split each dim evenly
    ReadOnlySpan<float> Mean { get; }

    // a unit vector for text into vector, or false (and zeros) when the model knows none of its tokens
    bool Encode(ReadOnlySpan<char> text, Span<float> vector);
}
