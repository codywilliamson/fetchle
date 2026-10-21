using System.Text;

namespace Fetchle.Core.Embedding;

// where the ids of a text go. a struct so the tokenizer is specialized per sink, no interface calls or boxing
interface ITokenSink
{
    void Add(int id);
}

// greedy longest-match-first WordPiece over one pre-tokenized word, like HF's WordPiece model: the first piece
// is looked up bare, later ones with the continuing prefix. when any piece is missing the whole word is [UNK],
// and an unknown word contributes nothing because model2vec drops [UNK] ids
static class WordPiece
{
    public static void Encode<TSink>(StaticModel model, scoped ReadOnlySpan<char> word, ref TSink sink)
        where TSink : ITokenSink, allows ref struct
    {
        var maxChars = model.MaxWordChars;
        Span<byte> bytes = stackalloc byte[maxChars * 4];
        Span<byte> key = stackalloc byte[maxChars * 4 + ModelFormat.MAX_PREFIX_BYTES];
        Span<int> pieces = stackalloc int[maxChars];
        var length = Encoding.UTF8.GetBytes(word, bytes);
        var prefix = model.Prefix;
        prefix.CopyTo(key);

        var count = 0;
        var start = 0;
        while (start < length)
        {
            var room = model.MaxTokenBytes - (start > 0 ? prefix.Length : 0);
            var end = Math.Min(length, start + room);
            while (end < length && end > start && IsContinuation(bytes[end]))
            {
                end--;
            }

            var id = -1;
            var keyStart = start > 0 ? 0 : prefix.Length;
            for (; end > start; end = PreviousBoundary(bytes, start, end))
            {
                var part = bytes[start..end];
                part.CopyTo(key[prefix.Length..]);
                id = model.Lookup(key[keyStart..(prefix.Length + part.Length)]);
                if (id >= 0)
                {
                    break;
                }
            }

            if (id < 0)
            {
                return;
            }

            pieces[count++] = id;
            start = end;
        }

        for (var i = 0; i < count; i++)
        {
            if (pieces[i] != model.UnkId)
            {
                sink.Add(pieces[i]);
            }
        }
    }

    static int PreviousBoundary(ReadOnlySpan<byte> bytes, int start, int end)
    {
        end--;
        while (end > start && IsContinuation(bytes[end]))
        {
            end--;
        }

        return end;
    }

    static bool IsContinuation(byte b) => (b & 0xC0) == 0x80;
}
