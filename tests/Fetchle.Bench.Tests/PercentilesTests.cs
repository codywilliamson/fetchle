using Fetchle.Bench;

namespace Fetchle.Bench.Tests;

public class PercentilesTests
{
    static readonly double[] OneToTwenty = Enumerable.Range(1, 20).Select(n => (double)n).ToArray();

    [Test]
    public async Task P50_of_twenty_runs_is_the_tenth()
    {
        await Assert.That(Percentiles.NearestRank(OneToTwenty, 50)).IsEqualTo(10);
    }

    [Test]
    public async Task P95_of_twenty_runs_is_the_nineteenth_not_the_slowest()
    {
        await Assert.That(Percentiles.NearestRank(OneToTwenty, 95)).IsEqualTo(19);
    }

    [Test]
    public async Task Input_order_does_not_matter()
    {
        double[] shuffled = [9, 1, 7, 3, 5, 2, 8, 4, 6, 10];
        await Assert.That(Percentiles.NearestRank(shuffled, 50)).IsEqualTo(5);
        await Assert.That(Percentiles.NearestRank(shuffled, 95)).IsEqualTo(10);
    }

    [Test]
    public async Task One_run_is_every_percentile()
    {
        await Assert.That(Percentiles.NearestRank([42.5], 50)).IsEqualTo(42.5);
        await Assert.That(Percentiles.NearestRank([42.5], 95)).IsEqualTo(42.5);
    }

    [Test]
    public async Task No_runs_throws()
    {
        await Assert.That(() => Percentiles.NearestRank([], 50)).Throws<ArgumentException>();
    }
}
