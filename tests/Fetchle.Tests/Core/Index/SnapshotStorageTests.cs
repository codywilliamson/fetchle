using Fetchle.Core.Index;
using Fetchle.Core.Search;
using Fetchle.Core.Walking;
using Fetchle.Fixtures;

namespace Fetchle.Tests.Core.Index;

// storage-engine checks: a damaged file is always a clean IndexUnreadableException, a dying writer never costs
// the old snapshot, and a swap works while readers still have the old file mapped
public class SnapshotStorageTests
{
    [Test]
    public async Task Every_truncation_is_unreadable()
    {
        using var tree = FixtureTree.Create(fillerFiles: 40);
        using var index = TempIndex.Built(tree.Root);
        var image = index.SnapshotBytes();

        for (var length = 0; length < image.Length; length++)
        {
            await AssertUnreadable(image[..length], index.Store.SnapshotPath);
        }
    }

    [Test]
    public async Task Every_corrupted_byte_is_unreadable()
    {
        using var tree = FixtureTree.Create(fillerFiles: 40);
        using var index = TempIndex.Built(tree.Root);
        var image = index.SnapshotBytes();

        for (var offset = 0; offset < image.Length; offset++)
        {
            foreach (var flip in (byte[])[0x01, 0x80, 0xFF])
            {
                var damaged = (byte[])image.Clone();
                damaged[offset] ^= flip;
                await AssertUnreadable(damaged, index.Store.SnapshotPath);
            }
        }
    }

    // past a matching checksum the structure checks are the last line: any byte value either fails them or
    // leaves a snapshot every query can run on without throwing
    [Test]
    public async Task Structure_checks_hold_without_the_checksum()
    {
        using var tree = FixtureTree.Create(fillerFiles: 40);
        using var index = TempIndex.Built(tree.Root);
        var image = index.SnapshotBytes();
        var request = new SearchRequest("e", [tree.Root], 50, TimeSpan.FromMinutes(1));
        var rejected = 0;

        for (var offset = SnapshotFormat.CHECKSUMMED_FROM; offset < image.Length; offset++)
        {
            foreach (var value in (byte[])[0x00, 0x01, 0x7F, 0x80, 0xFF])
            {
                var damaged = (byte[])image.Clone();
                damaged[offset] = value;
                using var snapshot = Snapshot.FromBytesSkippingChecksum(damaged);
                if (snapshot is null)
                {
                    rejected++;
                    continue;
                }

                var entry = SnapshotPaths.FindDirectory(snapshot, tree.Root);
                if (entry >= 0)
                {
                    var total = 0;
                    IndexQuery.Run(snapshot, request, [new IndexScope(entry, tree.Root)], new Fetchle.Core.Naive.TopHits(50), ref total, Deadline.After(TimeSpan.FromMinutes(1), System.Diagnostics.Stopwatch.GetTimestamp()));
                }
                _ = snapshot.Info();
            }
        }

        await Assert.That(rejected).IsGreaterThan(0);
    }

    [Test]
    public async Task Empty_wrong_version_and_wrong_endianness_are_unreadable()
    {
        using var tree = FixtureTree.Create(fillerFiles: 10);
        using var index = TempIndex.Built(tree.Root);
        var image = index.SnapshotBytes();

        await AssertUnreadable([], index.Store.SnapshotPath);
        var version = (byte[])image.Clone();
        version[SnapshotFormat.VERSION_OFFSET] = 2;
        await AssertUnreadable(version, index.Store.SnapshotPath, "version");
        var endian = (byte[])image.Clone();
        Array.Reverse(endian, SnapshotFormat.ENDIAN_OFFSET, sizeof(uint));
        await AssertUnreadable(endian, index.Store.SnapshotPath, "endianness");
    }

    [Test]
    public async Task Missing_snapshot_reads_as_null()
    {
        using var index = new TempIndex();
        await Assert.That(index.Store.ReadInfo()).IsNull();
    }

    [Test]
    [Arguments(nameof(RebuildStep.Walk))]
    [Arguments(nameof(RebuildStep.WriteTemp))]
    [Arguments(nameof(RebuildStep.Flush))]
    [Arguments(nameof(RebuildStep.Rename))]
    public async Task A_writer_dying_mid_swap_keeps_the_old_snapshot(string stepName)
    {
        var step = Enum.Parse<RebuildStep>(stepName);
        using var oldTree = FixtureTree.Create(seed: 1, fillerFiles: 30);
        using var newTree = FixtureTree.Create(seed: 2, fillerFiles: 300);
        using var index = TempIndex.Built(oldTree.Root);
        var before = index.Store.ReadInfo()!;

        var dying = new IndexStore(index.Dir) { BeforeStep = s => { if (s == step) { throw new IOException("simulated crash"); } } };
        await Assert.That(() => dying.Rebuild([newTree.Root], PruneRules.Default, CancellationToken.None)).Throws<IOException>();

        var after = index.Store.ReadInfo()!;
        await Assert.That(after.Roots[0].Path).IsEqualTo(before.Roots[0].Path);
        await Assert.That(after.Entries).IsEqualTo(before.Entries);

        // the next writer overwrites whatever temp file the dead one left and swaps cleanly
        var report = index.Store.Rebuild([newTree.Root], PruneRules.Default, CancellationToken.None);
        await Assert.That(index.Store.ReadInfo()!.Entries).IsEqualTo(report.Snapshot.Entries);
        await Assert.That(index.Store.ReadInfo()!.Roots[0].Path).IsEqualTo(Path.TrimEndingDirectorySeparator(newTree.Root));
    }

    [Test]
    public async Task A_torn_temp_file_is_never_read()
    {
        using var tree = FixtureTree.Create(fillerFiles: 30);
        using var index = TempIndex.Built(tree.Root);
        var image = index.SnapshotBytes();
        File.WriteAllBytes(index.Store.TempPath, image[..(image.Length / 2)]);

        await Assert.That(index.Store.ReadInfo()!.Entries).IsGreaterThan(0);
    }

    [Test]
    public async Task Swap_succeeds_while_a_reader_has_the_old_file_mapped()
    {
        using var oldTree = FixtureTree.Create(seed: 1, fillerFiles: 30);
        using var newTree = FixtureTree.Create(seed: 2, fillerFiles: 300);
        using var index = TempIndex.Built(oldTree.Root);

        using var reader = Snapshot.Open(index.Store.SnapshotPath)!;
        var oldEntries = reader.EntryCount;
        for (var swap = 0; swap < 3; swap++)
        {
            index.Store.Rebuild([newTree.Root], PruneRules.Default, CancellationToken.None);
        }

        // the old mapping still reads its own bytes, a fresh open sees the new file
        await Assert.That(reader.EntryCount).IsEqualTo(oldEntries);
        await Assert.That(reader.Info().Roots[0].Path).IsEqualTo(Path.TrimEndingDirectorySeparator(oldTree.Root));
        await Assert.That(index.Store.ReadInfo()!.Roots[0].Path).IsEqualTo(Path.TrimEndingDirectorySeparator(newTree.Root));
    }

    [Test]
    public async Task A_second_writer_is_refused_while_the_lock_is_held()
    {
        using var tree = FixtureTree.Create(fillerFiles: 10);
        using var index = new TempIndex();
        using var held = new FileStream(index.Store.LockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var store = new IndexStore(index.Dir) { LockWait = TimeSpan.FromMilliseconds(200) };

        await Assert.That(() => store.Rebuild([tree.Root], PruneRules.Default, CancellationToken.None)).Throws<IndexBusyException>();
    }

    [Test]
    public async Task A_rebuild_waits_out_a_lock_released_moments_later()
    {
        using var tree = FixtureTree.Create(fillerFiles: 10);
        using var index = new TempIndex();
        var held = new FileStream(index.Store.LockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var release = Task.Run(async () =>
        {
            await Task.Delay(300);
            held.Dispose();
        });

        var report = index.Store.Rebuild([tree.Root], PruneRules.Default, CancellationToken.None);
        await release;

        await Assert.That(report.Snapshot.Entries).IsGreaterThan(0);
    }

    [Test]
    public async Task Search_on_a_corrupt_snapshot_throws_unreadable_not_something_else()
    {
        using var tree = FixtureTree.Create(fillerFiles: 10);
        using var index = TempIndex.Built(tree.Root);
        var image = index.SnapshotBytes();
        image[^1] ^= 0xFF;
        File.WriteAllBytes(index.Store.SnapshotPath, image);
        var search = new IndexedFileSearch(PruneRules.Default, index.Store);

        await Assert.That(() => search.Search(new SearchRequest("e", [tree.Root], 5, TimeSpan.FromMinutes(1)), CancellationToken.None)).Throws<IndexUnreadableException>();
    }

    // in memory for the byte-level sweeps (thousands of cases), plus one real file per call through the mapped path
    static int _diskSample;

    static async Task AssertUnreadable(byte[] image, string path, string? reason = null)
    {
        IndexUnreadableException? caught = null;
        try
        {
            using var snapshot = Snapshot.FromBytes(image);
        }
        catch (IndexUnreadableException e)
        {
            caught = e;
        }

        await Assert.That(caught).IsNotNull();
        if (reason is not null)
        {
            await Assert.That(caught!.Reason).Contains(reason);
        }

        if (Interlocked.Increment(ref _diskSample) % 97 == 0 || reason is not null)
        {
            File.WriteAllBytes(path, image);
            await Assert.That(() => Snapshot.Open(path)?.Dispose()).Throws<IndexUnreadableException>();
        }
    }
}
