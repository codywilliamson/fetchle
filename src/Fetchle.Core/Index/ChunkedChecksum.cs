using System.IO.Hashing;

namespace Fetchle.Core.Index;

// the checksum of a mapped file every query opens: xxhash3 of each 1 MiB chunk, in parallel, then xxhash3 of those
// chunk hashes in order. one stream over a 30 MB mapping is page-fault bound on one thread; chunks fault in on every
// core at once. small inputs hash on the calling thread
static unsafe class ChunkedChecksum
{
    const int CHUNK = 1 << 20;
    // below this many chunks a parallel loop costs more than it saves
    const int PARALLEL_CHUNKS = 4;

    public static ulong Compute(ReadOnlySpan<byte> data)
    {
        var chunks = (int)((data.Length + (long)CHUNK - 1) / CHUNK);
        var hashes = new ulong[chunks];
        fixed (byte* start = data)
        {
            var at = (nint)start;
            var length = data.Length;
            if (chunks < PARALLEL_CHUNKS)
            {
                for (var c = 0; c < chunks; c++)
                {
                    hashes[c] = HashChunk(at, length, c);
                }
            }
            else
            {
                Parallel.For(0, chunks, c => hashes[c] = HashChunk(at, length, c));
            }
        }
        return XxHash3.HashToUInt64(System.Runtime.InteropServices.MemoryMarshal.AsBytes(hashes.AsSpan()));
    }

    static ulong HashChunk(nint start, int length, int chunk)
    {
        var from = (long)chunk * CHUNK;
        var size = (int)Math.Min(CHUNK, length - from);
        return XxHash3.HashToUInt64(new ReadOnlySpan<byte>((byte*)start + from, size));
    }
}
