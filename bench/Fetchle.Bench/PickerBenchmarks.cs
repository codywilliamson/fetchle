using System.Globalization;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using Fetchle.Cli.Commands;
using Fetchle.Cli.Picker;
using Fetchle.Core.Index;
using Fetchle.Core.Ranking.Semantic;
using Fetchle.Core.Search;
using Fetchle.Core.Walking;
using Fetchle.Fixtures;

namespace Fetchle.Bench;

// the v0.2 picker target: from a keystroke to the frame that shows its results, p95 under one 60 Hz frame (16 ms), over
// a 1M-entry index. the picker runs for real on an in-memory terminal, so this is the ui loop, the latest-wins runner,
// the ranker and the diff renderer, minus the terminal emulator's own paint
static class PickerBench
{
    public const string QUERY = "windows terminal settings";
    public const int ENTRIES = HomeCorpus.DEFAULT_ENTRIES;
    // v2: snapshot format 2 and vectors beside it
    const string CORPUS_VERSION = "v2";
    static readonly string Root = Path.Join(Path.GetTempPath(), "fetchle-picker-bench-root");

    // the snapshot is built once per machine and mapped on later runs, like the real index
    public static SearchSession OpenSession(int entries)
    {
        // the search resolves its root on disk even though the snapshot answers it, so the dir exists and stays empty
        Directory.CreateDirectory(Root);
        var store = new IndexStore(Path.Join(Path.GetTempPath(), $"fetchle-picker-bench-{entries}-{CORPUS_VERSION}"));
        var model = BenchModel.Instance;
        if (!File.Exists(store.SnapshotPath) || !File.Exists(store.VectorsPath))
        {
            Directory.CreateDirectory(store.Directory);
            var tree = TreeFromPaths.Build(Root, HomeCorpus.Entries(entries));
            var image = SnapshotWriter.ToBytes(tree, DateTimeOffset.UnixEpoch);
            store.WriteAndSwap(image, store.TempPath, store.SnapshotPath);
            using var snapshot = Snapshot.FromBytes(image);
            var vectors = VectorWriter.ToBytes(VectorBuilder.Build(snapshot, model), snapshot.Checksum, model.Checksum);
            store.WriteAndSwap(vectors, store.VectorsPath + IndexStore.TEMP_SUFFIX, store.VectorsPath);
        }
        return SearchSession.Open(store, PruneRules.Default, model);
    }

    public static SearchRequest Template => new("", [Root], PickerCommand.RESULT_LIMIT, TimeSpan.FromSeconds(5));

    public static PickerDriver StartPicker(SearchSession session)
    {
        var template = Template;
        return new PickerDriver((query, ct) => session.Search(template with { Query = query }, ct, prefix: true), "", TimeProvider.System);
    }
}

[MemoryDiagnoser]
public class PickerBenchmarks
{
    SearchSession _session = null!;
    PickerDriver _picker = null!;

    [GlobalSetup]
    public void Setup() => _session = PickerBench.OpenSession(PickerBench.ENTRIES);

    [IterationSetup]
    public void StartPicker() => _picker = PickerBench.StartPicker(_session);

    [IterationCleanup]
    public void StopPicker() => _picker.Dispose();

    [GlobalCleanup]
    public void Cleanup() => _session.Dispose();

    // one benchmark invocation is a whole typed query, one repaint per character
    [Benchmark]
    public double TypeQuery()
    {
        var total = TimeSpan.Zero;
        foreach (var c in PickerBench.QUERY)
        {
            total += _picker.PressAndWaitForPaint(c) ?? TimeSpan.Zero;
        }
        return total.TotalMilliseconds;
    }
}

// benchmarkdotnet reports means. the target is a p95, so this runs the same typing and keeps every keystroke
public static class PickerLatency
{
    const int WARMUP_PASSES = 3;
    const int MEASURED_PASSES = 40;
    const int P50 = 50;
    const int P95 = 95;

    public static int Run(string resultsDir)
    {
        using var session = PickerBench.OpenSession(PickerBench.ENTRIES);
        for (var pass = 0; pass < WARMUP_PASSES; pass++)
        {
            TypeOnce(session);
        }

        _unchanged = 0;
        var samples = new List<double>(MEASURED_PASSES * PickerBench.QUERY.Length);
        for (var pass = 0; pass < MEASURED_PASSES; pass++)
        {
            samples.AddRange(TypeOnce(session));
        }

        // the ranker alone on the same prefixes, to see how much of the keystroke is search and how much is ui
        var searchMs = SearchOnly(session);
        Console.WriteLine(FormattableString.Invariant($"search alone on the same prefixes: p50 {Percentiles.NearestRank(searchMs, P50):0.00} ms, p95 {Percentiles.NearestRank(searchMs, P95):0.00} ms"));
        var length = PickerBench.QUERY.Length;
        var perPrefix = Enumerable.Range(0, length).Select(i => FormattableString.Invariant($"{PickerBench.QUERY[..(i + 1)].TrimEnd()}={Percentiles.NearestRank(searchMs.Where((_, n) => n % length == i).ToList(), P50):0.0}"));
        Console.WriteLine("  p50 ms by prefix: " + string.Join(", ", perPrefix));

        var p50 = Percentiles.NearestRank(samples, P50);
        var p95 = Percentiles.NearestRank(samples, P95);
        var max = samples.Max();
        var os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macos" : "linux";
        Console.WriteLine(FormattableString.Invariant($"picker keystroke to repaint, {PickerBench.ENTRIES} entries, {os}, {samples.Count} repainted keystrokes ({_unchanged} left the screen unchanged): p50 {p50:0.00} ms, p95 {p95:0.00} ms, max {max:0.00} ms (target p95 <= 16 ms)"));

        var json = FormattableString.Invariant($"{{\"os\":\"{os}\",\"entries\":{PickerBench.ENTRIES},\"query\":\"{PickerBench.QUERY}\",\"keystrokes\":{samples.Count},\"unchanged\":{_unchanged},\"p50_ms\":{p50:0.000},\"p95_ms\":{p95:0.000},\"max_ms\":{max:0.000}}}");
        File.WriteAllText(Path.Combine(resultsDir, $"picker-latency-{os}.json"), json + "\n");
        return 0;
    }

    static List<double> SearchOnly(SearchSession session)
    {
        var template = PickerBench.Template;
        var ms = new List<double>(MEASURED_PASSES * PickerBench.QUERY.Length);
        for (var pass = 0; pass < MEASURED_PASSES; pass++)
        {
            for (var length = 1; length <= PickerBench.QUERY.Length; length++)
            {
                var start = System.Diagnostics.Stopwatch.GetTimestamp();
                session.Search(template with { Query = PickerBench.QUERY[..length] }, CancellationToken.None, prefix: true);
                ms.Add(System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            }
        }
        return ms;
    }

    // keystrokes whose results rendered the same cells write no frame, so they have no repaint to time and are only counted
    static List<double> TypeOnce(SearchSession session)
    {
        using var picker = PickerBench.StartPicker(session);
        var ms = new List<double>(PickerBench.QUERY.Length);
        foreach (var c in PickerBench.QUERY)
        {
            if (picker.PressAndWaitForPaint(c) is { } painted)
            {
                ms.Add(painted.TotalMilliseconds);
            }
            else
            {
                Interlocked.Increment(ref _unchanged);
            }
        }
        return ms;
    }

    static long _unchanged;
}
