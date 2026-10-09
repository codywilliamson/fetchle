using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Fetchle.Core.Search;
using Fetchle.Core.Walking;

namespace Fetchle.Bench;

// how the parallel walker scales with workers, per lister. a lister that's cheaper per dir
// single-threaded can still tie in parallel if something in the kernel serializes the opens
[MemoryDiagnoser]
public class WalkScalingBenchmarks
{
    string _root = "";
    FastWalker _walker = null!;

    [ParamsSource(nameof(Shapes))]
    public string Shape { get; set; } = BenchCorpus.REALISTIC;

    public static IEnumerable<string> Shapes => BenchCorpus.Shapes;

    [Params(1, 8, 16, 32)]
    public int Workers { get; set; }

    [Params(DOTNET, NATIVE)]
    public string Lister { get; set; } = DOTNET;

    const string DOTNET = "dotnet";
    const string NATIVE = "native";

    [GlobalSetup]
    public void Setup()
    {
        _root = BenchCorpus.Ensure(Shape);
        _walker = new FastWalker(PruneRules.Default) { WorkerCount = Workers, Lister = Lister == NATIVE ? ListerKind.Native : ListerKind.DotNet };
    }

    [Benchmark]
    public int FastWalk()
    {
        var counters = new ConcurrentBag<StrongBox<int>>();
        _walker.Walk(_root, () =>
        {
            var counter = new StrongBox<int>();
            counters.Add(counter);
            return (ref _) => counter.Value++;
        }, Deadline.After(TimeSpan.MaxValue, 0));

        var count = 0;
        foreach (var counter in counters)
        {
            count += counter.Value;
        }

        return count;
    }
}
