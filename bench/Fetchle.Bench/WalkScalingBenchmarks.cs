using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Fetchle.Core.Search;
using Fetchle.Core.Walking;

namespace Fetchle.Bench;

// how the parallel walker scales with workers. the walk waits on the os, not the cpu,
// so the best count isn't obviously the core count
[MemoryDiagnoser]
public class WalkScalingBenchmarks
{
    string _root = "";
    FastWalker _walker = null!;

    [ParamsSource(nameof(Shapes))]
    public string Shape { get; set; } = BenchCorpus.REALISTIC;

    public static IEnumerable<string> Shapes => BenchCorpus.Shapes;

    [Params(1, 4, 8, 16, 32, 64)]
    public int Workers { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _root = BenchCorpus.Ensure(Shape);
        _walker = new FastWalker(PruneRules.Default) { WorkerCount = Workers };
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
