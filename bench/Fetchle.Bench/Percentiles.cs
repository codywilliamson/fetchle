namespace Fetchle.Bench;

public static class Percentiles
{
    // nearest rank, in integer math so 95% of 20 runs is exactly the 19th, never 19.000000001 rounded up to the 20th
    public static double NearestRank(IReadOnlyList<double> values, int percent)
    {
        if (values.Count == 0)
        {
            throw new ArgumentException("no values", nameof(values));
        }
        var sorted = values.Order().ToArray();
        var rank = (percent * sorted.Length + 99) / 100;
        return sorted[Math.Max(1, rank) - 1];
    }
}
