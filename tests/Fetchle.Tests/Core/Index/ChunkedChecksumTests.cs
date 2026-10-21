using System.IO.Hashing;
using System.Runtime.InteropServices;
using Fetchle.Core.Index;

namespace Fetchle.Tests.Core.Index;

public class ChunkedChecksumTests
{
    static ulong Reference(byte[] data)
    {
        const int CHUNK = 1 << 20;
        var hashes = new List<ulong>();
        for (var at = 0; at < data.Length; at += CHUNK)
        {
            hashes.Add(XxHash3.HashToUInt64(data.AsSpan(at, Math.Min(CHUNK, data.Length - at))));
        }
        return XxHash3.HashToUInt64(MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(hashes)));
    }

    [Test]
    [Arguments(0)]
    [Arguments(100)]
    [Arguments(3 * (1 << 20) + 7)]
    [Arguments(9 * (1 << 20) + 5)]
    public async Task Matches_xxhash3_of_the_chunk_hashes_on_both_the_serial_and_parallel_paths(int length)
    {
        var data = new byte[length];
        new Random(length).NextBytes(data);
        await Assert.That(ChunkedChecksum.Compute(data)).IsEqualTo(Reference(data));
    }

    [Test]
    public async Task One_flipped_byte_in_a_late_chunk_changes_it()
    {
        var data = new byte[9 * (1 << 20)];
        new Random(1).NextBytes(data);
        var before = ChunkedChecksum.Compute(data);
        data[8 * (1 << 20) + 12] ^= 1;
        await Assert.That(ChunkedChecksum.Compute(data)).IsNotEqualTo(before);
    }
}
