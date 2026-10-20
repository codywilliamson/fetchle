using System.Diagnostics;
using Fetchle.E2E.Harness;
using Fetchle.Fixtures;

namespace Fetchle.E2E.Cli;

// roots, index rebuild/status and search through the snapshot, against the native exe
public class IndexTests
{
    const string SNAPSHOT = "index/snapshot.fidx";

    [Test]
    public async Task Rebuild_then_search_reads_the_snapshot()
    {
        using var tree = FixtureTree.Create();
        using var data = new TempDir();
        await Run(data, "roots", "add", tree.Root);
        var rebuild = await Run(data, "index", "rebuild");
        await Assert.That(rebuild.Stdout).Contains("indexed");

        // a file made after the rebuild isn't in the snapshot, so only a live walk can see it
        File.WriteAllText(tree.Full("src/app/after-rebuild.txt"), "");
        var indexed = await FetchleProcess.RunAsync(["after-rebuild"], dataDir: data.Path);
        await Assert.That(indexed.ExitCode).IsEqualTo(1);

        var found = await FetchleProcess.RunAsync(["settings"], dataDir: data.Path);
        await Assert.That(found.ExitCode).IsEqualTo(0);
        await Assert.That(found.Lines).IsEquivalentTo([tree.Full(FixtureTree.SETTINGS_FILE)]);

        var status = await Run(data, "index", "status");
        await Assert.That(status.Stdout).Contains(tree.Root);
    }

    [Test]
    public async Task A_root_not_yet_indexed_is_walked_live()
    {
        using var tree = FixtureTree.Create();
        using var data = new TempDir();
        await Run(data, "roots", "add", tree.Root);

        var run = await FetchleProcess.RunAsync(["settings"], dataDir: data.Path);

        await Assert.That(run.ExitCode).IsEqualTo(0);
        await Assert.That(run.Lines).IsEquivalentTo([tree.Full(FixtureTree.SETTINGS_FILE)]);
    }

    [Test]
    public async Task Status_without_an_index_is_exit_3()
    {
        using var data = new TempDir();
        var run = await FetchleProcess.RunAsync(["index", "status"], dataDir: data.Path);
        await Assert.That(run.ExitCode).IsEqualTo(3);
        await Assert.That(run.Stderr).Contains("fetchle index rebuild");
    }

    [Test]
    public async Task Rebuild_without_roots_is_a_usage_error()
    {
        using var data = new TempDir();
        await Assert.That((await FetchleProcess.RunAsync(["index", "rebuild"], dataDir: data.Path)).ExitCode).IsEqualTo(2);
    }

    [Test]
    public async Task Roots_add_list_remove_round_trip()
    {
        using var tree = FixtureTree.Create();
        using var data = new TempDir();
        await Run(data, "roots", "add", tree.Root);
        await Assert.That((await Run(data, "roots", "list")).Lines).IsEquivalentTo([tree.Root]);

        // nested either way is refused, so the snapshot never holds an entry twice
        await Assert.That((await FetchleProcess.RunAsync(["roots", "add", tree.Full("src")], dataDir: data.Path)).ExitCode).IsEqualTo(2);
        await Assert.That((await FetchleProcess.RunAsync(["roots", "add", Path.GetDirectoryName(tree.Root)!], dataDir: data.Path)).ExitCode).IsEqualTo(2);
        await Assert.That((await FetchleProcess.RunAsync(["roots", "add", tree.Full("nope")], dataDir: data.Path)).ExitCode).IsEqualTo(2);

        await Run(data, "roots", "remove", tree.Root);
        await Assert.That((await Run(data, "roots", "list")).Lines).IsEmpty();
    }

    [Test]
    [Arguments("settings")]
    [Arguments("roots", "list")]
    [Arguments("index", "status")]
    [Arguments("index", "rebuild")]
    [Arguments("doctor")]
    public async Task A_corrupt_config_is_a_clean_usage_error(params string[] args)
    {
        using var data = new TempDir();
        Directory.CreateDirectory(data.Path);
        File.WriteAllText(Path.Combine(data.Path, "config.json"), "{\"roots\": [");

        var run = await FetchleProcess.RunAsync(args, dataDir: data.Path);

        await Assert.That(run.ExitCode).IsEqualTo(2).Because(run.Stderr);
        await Assert.That(run.Stderr).Contains("config.json");
        await Assert.That(run.Stderr).DoesNotContain("   at ");
    }

    [Test]
    [Arguments("{\"roots\": [null]}")]
    [Arguments("{\"roots\": [\"\"]}")]
    [Arguments("{}")]
    public async Task A_config_with_a_null_or_missing_root_list_is_a_clean_usage_error(string json)
    {
        using var data = new TempDir();
        Directory.CreateDirectory(data.Path);
        File.WriteAllText(Path.Combine(data.Path, "config.json"), json);

        var run = await FetchleProcess.RunAsync(["index", "status"], dataDir: data.Path);

        await Assert.That(run.ExitCode).IsEqualTo(2).Because(run.Stderr);
        await Assert.That(run.Stderr).DoesNotContain("   at ");
    }

    [Test]
    public async Task A_damaged_snapshot_is_exit_3_at_every_sampled_offset()
    {
        using var tree = FixtureTree.Create(fillerFiles: 20);
        using var data = new TempDir();
        await Run(data, "roots", "add", tree.Root);
        await Run(data, "index", "rebuild");
        var snapshot = Path.Combine(data.Path, SNAPSHOT);
        var image = File.ReadAllBytes(snapshot);

        // the unit tests sweep every offset in process; here a spread of them goes through the real exe
        var offsets = new SortedSet<int> { 0, 7, 8, 12, 16, 24, 32, 40, 64, 191, image.Length / 2, image.Length - 1 };
        var rng = new Random(7);
        while (offsets.Count < 32)
        {
            offsets.Add(rng.Next(image.Length));
        }

        foreach (var offset in offsets)
        {
            var damaged = (byte[])image.Clone();
            damaged[offset] ^= 0xFF;
            File.WriteAllBytes(snapshot, damaged);
            await AssertExit3(data, $"corrupt byte {offset}");

            File.WriteAllBytes(snapshot, image[..offset]);
            await AssertExit3(data, $"truncated to {offset}");
        }
    }

    [Test]
    public async Task Killing_a_rebuild_at_any_point_keeps_a_readable_index()
    {
        using var small = FixtureTree.Create(seed: 1, fillerFiles: 20);
        using var big = FixtureTree.Create(seed: 2, fillerFiles: 8000);
        using var data = new TempDir();
        await Run(data, "roots", "add", small.Root);
        await Run(data, "index", "rebuild");
        await Run(data, "roots", "remove", small.Root);
        await Run(data, "roots", "add", big.Root);

        var timer = Stopwatch.StartNew();
        await Run(data, "index", "rebuild");
        var full = timer.Elapsed;
        await Run(data, "roots", "remove", big.Root);
        await Run(data, "roots", "add", small.Root);
        await Run(data, "index", "rebuild");
        await Run(data, "roots", "remove", small.Root);
        await Run(data, "roots", "add", big.Root);

        for (var i = 0; i < 12; i++)
        {
            var delay = TimeSpan.FromTicks(full.Ticks * i / 10);
            using var process = Process.Start(new ProcessStartInfo(NativeExe.Path, ["index", "rebuild"])
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Environment = { ["FETCHLE_DATA_DIR"] = data.Path },
            })!;
            await Task.Delay(delay);
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();

            var status = await FetchleProcess.RunAsync(["index", "status"], dataDir: data.Path);
            await Assert.That(status.ExitCode).IsEqualTo(0).Because($"killed after {delay.TotalMilliseconds:0} ms: {status.Stderr}");
        }

        // and the next rebuild finishes cleanly over whatever the killed ones left
        await Run(data, "index", "rebuild");
        await Assert.That((await Run(data, "index", "status")).Stdout).Contains(big.Root);
    }

    static async Task AssertExit3(TempDir data, string because)
    {
        var search = await FetchleProcess.RunAsync(["settings"], dataDir: data.Path);
        await Assert.That(search.ExitCode).IsEqualTo(3).Because($"search, {because}: {search.Stderr}");
        await Assert.That(search.Stderr).Contains("fetchle index rebuild");
        var status = await FetchleProcess.RunAsync(["index", "status"], dataDir: data.Path);
        await Assert.That(status.ExitCode).IsEqualTo(3).Because($"status, {because}: {status.Stderr}");
    }

    static async Task<CliRun> Run(TempDir data, params string[] args)
    {
        var run = await FetchleProcess.RunAsync(args, dataDir: data.Path);
        await Assert.That(run.ExitCode).IsEqualTo(0).Because($"fetchle {string.Join(' ', args)}: {run.Stderr}");
        return run;
    }
}

sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"fetchle-e2e-{Guid.NewGuid():N}");

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
