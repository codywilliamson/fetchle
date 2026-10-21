using BenchmarkDotNet.Attributes;
using Fetchle.Core.Embedding;

namespace Fetchle.Bench;

// the embedding hot path: one Encode per path segment, and what opening the model costs.
// needs the real model: `dotnet build.cs model`, or point FETCHLE_MODEL at a .fmodel
[MemoryDiagnoser]
public class EncodeBenchmarks
{
    const int PHRASES = 10_000;
    const int SEED = 42;

    static readonly string[] Stems =
    [
        "Microsoft.WindowsTerminal", "ConsoleHost_history", "claude", "node_modules", "package-lock", "settings", "Local State",
        "Google Chrome", "Code Cache", "workspaceStorage", "appsettings.Development", "Program Files", "user data", "Cookies",
        "IMG_20240615_183045", "Screenshot 2026-03-14", "Invoice_2026-08_final", "Meeting Notes", "Budget", "Q3 Planning Deck",
        "tsconfig.base", "vite.config", "Directory.Build", "Fetchle.Core", "SnapshotWriter", "FastWalker", "obj", "bin", "net10.0",
    ];

    static readonly string[] Suffixes = ["", ".json", ".txt", ".exe", ".dll", ".md", ".png", ".log", "_v2", "-backup", ".ts", ".cs", "_8wekyb3d8bbwe"];

    string[] _phrases = null!;
    string _path = "";
    StaticModel _model = null!;
    float[] _vector = null!;

    [GlobalSetup]
    public void Setup()
    {
        _path = ModelLocation.Find() ?? throw new FileNotFoundException("no model, run `dotnet build.cs model` or set FETCHLE_MODEL");
        _model = StaticModel.Open(_path) ?? throw new FileNotFoundException(_path);
        _vector = new float[_model.Dimensions];
        _phrases = Phrases();
    }

    [GlobalCleanup]
    public void Cleanup() => _model.Dispose();

    [Benchmark(OperationsPerInvoke = PHRASES)]
    public int EncodeSegments()
    {
        var known = 0;
        foreach (var phrase in _phrases)
        {
            if (_model.Encode(phrase, _vector))
            {
                known++;
            }
        }

        return known;
    }

    // map, validate (checksum over the whole file) and release, with the file already in the page cache
    [Benchmark]
    public int Open()
    {
        using var model = StaticModel.Open(_path)!;
        return model.Dimensions;
    }

    static string[] Phrases()
    {
        var random = new Random(SEED);
        var phrases = new string[PHRASES];
        for (var i = 0; i < phrases.Length; i++)
        {
            phrases[i] = Stems[random.Next(Stems.Length)] + Suffixes[random.Next(Suffixes.Length)] + (random.Next(3) == 0 ? " " + random.Next(1, 3000) : "");
        }

        return phrases;
    }
}
