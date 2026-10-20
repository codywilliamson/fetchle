namespace Fetchle.Fixtures;

enum FileKind : byte { Code, Media, Document, Data, Npm, Hex, GitObject }

// how one kind of directory subtree is shaped: files per dir, name words, how deep it nests
sealed record HomeProfile(FileKind Kind, double MedianFiles, double Sigma, double BulkChance, int BulkMin, int BulkMax, double Skew, int MaxDepth, string[] DirWords)
{
    // skew > 1 piles new dirs under early (shallow) ones, < 1 pushes them deeper
    public static readonly HomeProfile Code = new(FileKind.Code, 4, 1.0, 0.002, 500, 3000, 2.2, 14, HomeWords.CodeDirs);
    public static readonly HomeProfile AppData = new(FileKind.Data, 3, 1.3, 0.004, 800, 6000, 2.0, 16, HomeWords.AppDirs);
    public static readonly HomeProfile Media = new(FileKind.Media, 8, 1.2, 0.005, 500, 4000, 2.5, 8, HomeWords.MediaDirs);
    public static readonly HomeProfile Documents = new(FileKind.Document, 6, 1.2, 0.003, 300, 1500, 2.0, 8, HomeWords.MediaDirs);
    public static readonly HomeProfile Npm = new(FileKind.Npm, 5, 0.9, 0.001, 400, 2500, 1.8, 16, HomeWords.NpmDirs);
    public static readonly HomeProfile Cache = new(FileKind.Hex, 40, 1.3, 0.03, 1000, 8000, 3.0, 6, HomeWords.CacheDirs);
    public static readonly HomeProfile Packages = new(FileKind.Code, 4, 1.1, 0.0015, 400, 2000, 2.0, 12, HomeWords.NpmDirs);

    // lognormal with the profile's median, plus a rare log-uniform bulk dir with thousands of files
    public int SampleFileCount(Random rng)
    {
        if (rng.NextDouble() < BulkChance)
        {
            return (int)(BulkMin * Math.Pow((double)BulkMax / BulkMin, rng.NextDouble()));
        }
        var z = Math.Sqrt(-2 * Math.Log(1 - rng.NextDouble())) * Math.Cos(2 * Math.PI * rng.NextDouble());
        return (int)Math.Floor(Math.Exp(Math.Log(MedianFiles) + Sigma * z));
    }

    public string SampleDirName(Random rng)
    {
        var word = DirWords[rng.Next(DirWords.Length)];
        return rng.Next(10) switch
        {
            < 4 => word,
            < 7 => $"{word}-{DirWords[rng.Next(DirWords.Length)]}",
            < 9 => $"{word}{rng.Next(1, 40)}",
            _ => $"{word}.{rng.Next(1, 10)}",
        };
    }
}
