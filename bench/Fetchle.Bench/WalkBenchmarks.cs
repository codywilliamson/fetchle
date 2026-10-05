using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Fetchle.Core.Naive;
using Fetchle.Core.Search;
using Fetchle.Core.Walking;

namespace Fetchle.Bench;

[MemoryDiagnoser]
public class WalkBenchmarks
{
    string _root = "";
    readonly NaiveWalker _walker = new(PruneRules.Default);
    readonly FastWalker _dotNetWalker = new(PruneRules.Default) { Lister = ListerKind.DotNet };
    readonly FastWalker _nativeWalker = new(PruneRules.Default) { Lister = ListerKind.Native };
    int _count;

    [ParamsSource(nameof(Shapes))]
    public string Shape { get; set; } = BenchCorpus.REALISTIC;

    public static IEnumerable<string> Shapes => BenchCorpus.Shapes;

    [GlobalSetup]
    public void Setup() => _root = BenchCorpus.Ensure(Shape);

    [Benchmark]
    public int NaiveWalk()
    {
        _count = 0;
        _walker.Walk(_root, (ref _) => _count++, Deadline.After(TimeSpan.MaxValue, 0));
        return _count;
    }

    [Benchmark]
    public int FastWalkDotNet() => CountWith(_dotNetWalker);

    // on a machine that can't run it the factory falls back to the .NET lister
    [Benchmark]
    public int FastWalkNative() => CountWith(_nativeWalker);

    int CountWith(FastWalker fastWalker)
    {
        // a private counter per worker, summed after, so counting adds no contention
        var counters = new ConcurrentBag<StrongBox<int>>();
        fastWalker.Walk(_root, () =>
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
