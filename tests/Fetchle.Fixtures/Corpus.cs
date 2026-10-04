namespace Fetchle.Fixtures;

// the generated fake home dir that bench and evals search: real app files, near-miss distractors, seeded filler
public static class Corpus
{
    public const int DEFAULT_SEED = 42;

    // eval targets and their near-miss distractors
    public static readonly string[] Landmarks =
    [
        "AppData/Roaming/Claude/claude-code/2.1.286/635c1867224a/claude.exe",
        "AppData/Roaming/Claude/claude-code/2.1.284/3f4bed3e44ad/claude.exe",
        "AppData/Roaming/Claude/claude_desktop_config.json",
        "AppData/Roaming/Microsoft/Windows/PowerShell/PSReadLine/ConsoleHost_history.txt",
        "AppData/Roaming/Code/User/settings.json",
        "AppData/Roaming/Code/User/keybindings.json",
        "AppData/Local/Packages/Microsoft.WindowsTerminal_8wekyb3d8bbwe/LocalState/settings.json",
        "AppData/Local/Packages/Microsoft.WindowsTerminal_8wekyb3d8bbwe/LocalState/state.json",
        "AppData/Local/Microsoft/Windows Terminal/Fragments/Git/git.json",
        "AppData/Local/Google/Chrome/User Data/Default/History",
        "AppData/Local/Google/Chrome/User Data/Default/Bookmarks",
        "AppData/Roaming/Mozilla/Firefox/Profiles/k2x9qv1a.default-release/places.sqlite",
        "AppData/Roaming/Mozilla/Firefox/Profiles/k2x9qv1a.default-release/favicons.sqlite",
        "AppData/Roaming/nvm/settings.txt",
        "AppData/Local/GitHubDesktop/GitHubDesktop.exe",
        "AppData/Local/GitHubDesktop/app-3.4.9/GitHubDesktop.exe",
        "AppData/Local/GitHubDesktop/app-3.4.9/resources/app/desktop-notifications.node",
        "AppData/Local/Discord/app-1.0.9163/Discord.exe",
        "AppData/Local/Discord/Update.exe",
        "AppData/Roaming/obsidian/obsidian.json",
        "AppData/Roaming/Thunderbird/Profiles/x7m2p0qd.default-release/global-messages-db.sqlite",
        "AppData/Roaming/Thunderbird/Profiles/x7m2p0qd.default-release/prefs.js",
        "AppData/Roaming/Spotify/Spotify.exe",
        "AppData/Roaming/Spotify/SpotifyStartupTask.exe",
        "AppData/Local/FiveM/FiveM.app/citizen/common/data/streaming_surrogate.rpf",
        "AppData/Roaming/npm/node_modules/claude-code/cli.js",
        "source/repos/fetchle/src/Fetchle.Core/NaiveWalker.cs",
        "source/repos/fetchle/node_modules/settings/index.js",
    ];

    static readonly string[] Vendors = ["Adobe", "JetBrains", "Microsoft", "Mozilla", "Google", "Steam", "Slack", "Zoom", "Docker", "Postman", "Figma", "Notion"];
    static readonly string[] Products = ["Shared", "Cache", "Logs", "Data", "Plugins", "Profiles", "Extensions", "Assets", "Config", "State"];
    static readonly string[] Words = ["alpha", "build", "delta", "event", "frame", "graph", "index", "layer", "model", "parse", "query", "route", "store", "token", "video", "audio", "chunk", "sheet", "theme", "panel"];
    static readonly string[] Extensions = [".json", ".log", ".dll", ".png", ".js", ".css", ".txt", ".xml", ".dat", ".db", ".cs", ".md"];
    static readonly string[] Tops = ["AppData/Local", "AppData/Roaming", "source/repos", "Documents", "Downloads"];

    // real trees average roughly this many files per directory; per-directory cost dominates a walk
    public const int REALISTIC_FILES_PER_DIRECTORY = 20;

    // one file per directory: a load test for the per-directory cost
    public const int DIR_HEAVY_FILES_PER_DIRECTORY = 1;

    // bump when the generated layout changes, so cached corpora on disk get rebuilt
    const int LAYOUT_VERSION = 2;

    public static string Ensure(string root, int fillerFiles, int filesPerDirectory = REALISTIC_FILES_PER_DIRECTORY, int seed = DEFAULT_SEED)
    {
        var marker = Path.Combine(root, $".corpus-v{LAYOUT_VERSION}-{seed}-{fillerFiles}-{filesPerDirectory}");
        if (File.Exists(marker))
        {
            return root;
        }

        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        foreach (var file in Landmarks)
        {
            Touch(root, file);
        }

        foreach (var file in Filler(seed, fillerFiles, filesPerDirectory))
        {
            Touch(root, file);
        }

        File.WriteAllText(marker, "");
        return root;
    }

    public static string DefaultRoot(int fillerFiles, int filesPerDirectory = REALISTIC_FILES_PER_DIRECTORY, int seed = DEFAULT_SEED) =>
        Path.Combine(Path.GetTempPath(), $"fetchle-corpus-{seed}-{fillerFiles}-{filesPerDirectory}");

    static IEnumerable<string> Filler(int seed, int count, int filesPerDirectory)
    {
        var rng = new Random(seed);
        // distinct leaf dirs, each getting exactly filesPerDirectory files round-robin
        var dirCount = Math.Max(1, count / filesPerDirectory);
        var dirs = new List<string>(dirCount);
        var seen = new HashSet<string>();
        while (dirs.Count < dirCount)
        {
            var dir = $"{Tops[rng.Next(Tops.Length)]}/{Vendors[rng.Next(Vendors.Length)]}/{Products[rng.Next(Products.Length)]}";
            for (var d = rng.Next(1, 5); d > 0; d--)
            {
                dir += $"/{Words[rng.Next(Words.Length)]}{rng.Next(4)}";
            }

            if (seen.Add(dir))
            {
                dirs.Add(dir);
            }
        }
        for (var i = 0; i < count; i++)
        {
            yield return $"{dirs[i % dirs.Count]}/{Words[rng.Next(Words.Length)]}_{i:D6}{Extensions[rng.Next(Extensions.Length)]}";
        }
    }

    static void Touch(string root, string relative)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "");
    }
}
