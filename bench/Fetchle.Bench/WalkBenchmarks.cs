using BenchmarkDotNet.Attributes;
using Fetchle.Core;

namespace Fetchle.Bench;

[MemoryDiagnoser]
public class WalkBenchmarks
{
    string _root = "";
    readonly NaiveWalker _walker = new(PruneRules.Default);
    int _count;

    [ParamsSource(nameof(Shapes))]
    public string Shape { get; set; } = BenchCorpus.Realistic;

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
}
