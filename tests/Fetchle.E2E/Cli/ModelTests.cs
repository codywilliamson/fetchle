using Fetchle.E2E.Harness;

namespace Fetchle.E2E.Cli;

// the release path for meaning: the published exe finds the model beside itself, a rebuild writes vectors, and a
// query that shares no word with the file still finds it
public class ModelTests
{
    const string MODEL = "potion-retrieval-32M.fmodel";

    static readonly string[] Files =
    [
        "AppData/Roaming/Spotify/Spotify.exe",
        "AppData/Local/FiveM/citizen/streaming_surrogate.rpf",
        "AppData/Roaming/Code/User/settings.json",
        "Documents/notes/todo.txt",
        "source/app/src/index.ts",
    ];

    static string MakeTree(TempDir dir)
    {
        foreach (var file in Files)
        {
            var path = System.IO.Path.Combine(dir.Path, file);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "");
        }
        return dir.Path;
    }

    [Test]
    public async Task The_published_exe_ships_with_its_model() =>
        await Assert.That(File.Exists(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(NativeExe.Path)!, MODEL))).IsTrue()
            .Because("dotnet build.cs model runs before publish, and the cli project copies the model beside the exe");

    [Test]
    public async Task Rebuild_embeds_names_and_a_query_finds_a_file_by_meaning()
    {
        using var tree = new TempDir();
        using var data = new TempDir();
        var root = MakeTree(tree);
        await FetchleProcess.RunAsync(["roots", "add", root], dataDir: data.Path);

        var rebuild = await FetchleProcess.RunAsync(["index", "rebuild"], dataDir: data.Path);
        await Assert.That(rebuild.ExitCode).IsEqualTo(0);
        await Assert.That(rebuild.Stdout).Contains("names embedded");
        await Assert.That(File.Exists(System.IO.Path.Combine(data.Path, "index", "vectors.fvec"))).IsTrue();

        var run = await FetchleProcess.RunAsync(["music streaming app"], dataDir: data.Path);
        await Assert.That(run.ExitCode).IsEqualTo(0);
        await Assert.That(run.Lines.Take(3)).Contains(System.IO.Path.Combine(root, "AppData", "Roaming", "Spotify", "Spotify.exe"));
    }

    [Test]
    public async Task Without_the_model_search_still_ranks_by_words()
    {
        using var tree = new TempDir();
        using var data = new TempDir();
        var root = MakeTree(tree);
        var noModel = new Dictionary<string, string> { ["FETCHLE_MODEL"] = System.IO.Path.Combine(data.Path, "no-such.fmodel") };
        await FetchleProcess.RunAsync(["roots", "add", root], dataDir: data.Path, environment: noModel);

        var rebuild = await FetchleProcess.RunAsync(["index", "rebuild"], dataDir: data.Path, environment: noModel);
        await Assert.That(rebuild.Stdout).Contains("no model");

        var run = await FetchleProcess.RunAsync(["settings json"], dataDir: data.Path, environment: noModel);
        await Assert.That(run.ExitCode).IsEqualTo(0);
        await Assert.That(run.Lines[0]).IsEqualTo(System.IO.Path.Combine(root, "AppData", "Roaming", "Code", "User", "settings.json"));
    }

    [Test]
    public async Task An_unrelated_query_finds_nothing()
    {
        using var tree = new TempDir();
        using var data = new TempDir();
        var root = MakeTree(tree);

        var run = await FetchleProcess.RunAsync(["qwzx vbnm", "--root", root], dataDir: data.Path);
        await Assert.That(run.ExitCode).IsEqualTo(1);
        await Assert.That(run.Stdout).IsEmpty();
    }
}
