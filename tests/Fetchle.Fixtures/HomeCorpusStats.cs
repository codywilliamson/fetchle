namespace Fetchle.Fixtures;

// what the home corpus holds, counted from the plan. the root dir itself is not an entry
public sealed record HomeCorpusStats(int Dirs, int Files, int PrunedDirs, int PrunedBelow, int MaxDepth)
{
    const int MARKER_FIELDS = 5;

    public int Entries => Dirs + Files;

    // entries inside a default-pruned dir (node_modules, .git, .cache), not counting the pruned dir itself
    public double PrunedShare => (double)PrunedBelow / Entries;

    public string ToMarker() => $"{Dirs} {Files} {PrunedDirs} {PrunedBelow} {MaxDepth}";

    public static HomeCorpusStats? FromMarker(string text)
    {
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != MARKER_FIELDS || !parts.All(p => int.TryParse(p, out _)))
        {
            return null;
        }
        return new HomeCorpusStats(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), int.Parse(parts[3]), int.Parse(parts[4]));
    }
}
