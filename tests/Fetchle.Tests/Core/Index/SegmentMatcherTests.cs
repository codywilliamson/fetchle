using System.Diagnostics;
using System.Text;
using Fetchle.Core.Index;
using Fetchle.Core.Search;

namespace Fetchle.Tests.Core.Index;

public class SegmentMatcherTests
{
    static readonly string Root = Path.Combine(Path.GetTempPath(), "fetchle-segment-matcher");

    static Snapshot BuildSnapshot(int files)
    {
        var collector = new TreeCollector();
        var rng = new Random(3);
        for (var i = 0; i < files; i++)
        {
            var name = i % 997 == 0 ? $"Settings{i}.json" : $"{(char)('a' + rng.Next(26))}{rng.Next(1_000_000)}_{i}{(i % 5 == 0 ? ".txt" : "")}";
            collector.Add(Root, name, isDirectory: false);
        }

        var tree = TreeAssembler.Assemble([new RootWalk(Root, [collector], TimeSpan.Zero)]);
        return Snapshot.FromBytes(SnapshotWriter.ToBytes(tree, DateTimeOffset.UnixEpoch));
    }

    // chunks far smaller than any real blob, so a match lands on, before and after many chunk edges
    [Test]
    [Arguments("settings")]
    [Arguments("SETTINGS1")]
    [Arguments(".txt")]
    [Arguments("_99")]
    [Arguments("zzz")]
    [Arguments("a1")]
    public async Task Chunked_scan_marks_the_same_segments_as_string_contains(string query)
    {
        using var snapshot = BuildSnapshot(20_000);

        foreach (var chunk in (int[])[1, 64, 4096, 1 << 20])
        {
            var bits = SegmentMatcher.Match(snapshot, query, Deadline.After(TimeSpan.FromMinutes(5), Stopwatch.GetTimestamp()), out var any, out var completed, chunk);
            try
            {
                await Assert.That(completed).IsTrue();
                var expectedAny = false;
                for (var segment = 0u; segment < (uint)snapshot.SegmentCount; segment++)
                {
                    var expected = Encoding.UTF8.GetString(snapshot.Segment(segment)).Contains(query, StringComparison.OrdinalIgnoreCase);
                    expectedAny |= expected;
                    await Assert.That(SegmentMatcher.IsSet(bits, segment)).IsEqualTo(expected).Because($"segment {segment}, chunk {chunk}");
                }

                await Assert.That(any).IsEqualTo(expectedAny);
            }
            finally
            {
                System.Buffers.ArrayPool<ulong>.Shared.Return(bits);
            }
        }
    }

    [Test]
    [Arguments("zzz")]
    [Arguments("settings")]
    [Arguments("café")]
    public async Task An_expired_deadline_stops_the_scan_and_says_so(string query)
    {
        using var snapshot = BuildSnapshot(20_000);
        var expired = Deadline.After(TimeSpan.Zero, Stopwatch.GetTimestamp());

        var bits = SegmentMatcher.Match(snapshot, query, expired, out _, out var completed);
        System.Buffers.ArrayPool<ulong>.Shared.Return(bits);

        await Assert.That(completed).IsFalse();
    }
}
