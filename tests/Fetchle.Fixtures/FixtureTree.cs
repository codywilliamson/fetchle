using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Fetchle.Fixtures;

// a temp tree with the things that break walkers, docs/testing.md#end-to-end-against-fixture-trees.
// same seed, same tree. features the os or user can't create are left out and reported in Created
public sealed class FixtureTree : IDisposable
{
    public const string SettingsFile = "src/app/Settings.json";
    public const string PrunedSettingsFile = "node_modules/pkg/node_modules/inner/lib/settings.json";
    public const string GitSettingsFile = ".git/settings";
    public const string JunctionDir = "links/to-src";
    public const string LoopDir = "links/loop";
    public const string LockedFile = "locked/secret-locked.txt";
    public const string CaseLower = "case/readme-collide.md";
    public const string CaseUpper = "case/README-COLLIDE.md";
    public const string LongPathMarker = "deep-target.txt";

    public static readonly string[] UnicodeFiles = ["unicode/café.txt", "unicode/日本語.md", "unicode/🦊 fox notes.txt"];

    public string Root { get; }
    public FixtureFeatures Created { get; private set; }
    public string LongPathFile { get; private set; } = "";
    public IReadOnlyList<string> FillerFiles { get; }

    FixtureTree(string root, IReadOnlyList<string> filler)
    {
        Root = root;
        FillerFiles = filler;
    }

    public static FixtureTree Create(int seed = 42, int fillerFiles = 200)
    {
        var root = Path.Combine(ResolveLinks(Path.GetTempPath()), $"fetchle-fixture-{seed}-{Guid.NewGuid():N}");
        var filler = Filler(seed, fillerFiles);
        var tree = new FixtureTree(root, filler);
        foreach (var file in (string[])[SettingsFile, "src/app/main.cs", "docs/readme.md", PrunedSettingsFile, GitSettingsFile, .. UnicodeFiles, .. filler])
            tree.Touch(file);
        tree.CreateLongPath();
        tree.CreateLinks();
        tree.CreateLocked();
        tree.CreateCaseCollision();
        return tree;
    }

    // macos temp is under /var, a symlink to /private/var, and a child process started there
    // reports the resolved path as its working directory
    static string ResolveLinks(string path)
    {
        var resolved = Path.GetPathRoot(path)!;
        foreach (var segment in path[resolved.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            resolved = Path.Combine(resolved, segment);
            if (Directory.ResolveLinkTarget(resolved, returnFinalTarget: true) is { } target) resolved = target.FullName;
        }
        return resolved;
    }

    public string Full(string relative) => Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

    void Touch(string relative)
    {
        var path = Full(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, relative);
    }

    void CreateLongPath()
    {
        var segments = new List<string> { "long" };
        while (Full(string.Join('/', segments)).Length < 300) segments.Add(new string('d', 40) + segments.Count);
        segments.Add(LongPathMarker);
        LongPathFile = string.Join('/', segments);
        Touch(LongPathFile);
        Created |= FixtureFeatures.LongPath;
    }

    void CreateLinks()
    {
        Directory.CreateDirectory(Full("links"));
        if (TryLink(Full(JunctionDir), Full("src"))) Created |= FixtureFeatures.Junction;
        if (TryLink(Full(LoopDir), Root)) Created |= FixtureFeatures.SymlinkLoop;
    }

    // a junction on windows (no privilege needed), a directory symlink elsewhere
    static bool TryLink(string link, string target)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                Directory.CreateSymbolicLink(link, target);
                return true;
            }
            using var p = Process.Start(new ProcessStartInfo("cmd.exe", ["/c", "mklink", "/J", link, target]) { RedirectStandardOutput = true, RedirectStandardError = true })!;
            p.WaitForExit();
            return p.ExitCode == 0;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    void CreateLocked()
    {
        Touch(LockedFile);
        var dir = Full("locked");
        if (OperatingSystem.IsWindows())
        {
            var info = new DirectoryInfo(dir);
            var acl = info.GetAccessControl();
            acl.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.ListDirectory, AccessControlType.Deny));
            info.SetAccessControl(acl);
        }
        else
        {
            File.SetUnixFileMode(dir, UnixFileMode.None);
        }
        // root ignores permissions, so only claim it when listing really fails
        try
        {
            Directory.EnumerateFileSystemEntries(dir).GetEnumerator().MoveNext();
        }
        catch (UnauthorizedAccessException)
        {
            Created |= FixtureFeatures.Unreadable;
        }
    }

    void Unlock()
    {
        var dir = Full("locked");
        if (!Directory.Exists(dir)) return;
        if (OperatingSystem.IsWindows())
        {
            var info = new DirectoryInfo(dir);
            var acl = info.GetAccessControl();
            acl.RemoveAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.ListDirectory, AccessControlType.Deny));
            info.SetAccessControl(acl);
        }
        else
        {
            File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    void CreateCaseCollision()
    {
        Touch(CaseLower);
        // on a case-insensitive file system the second name is the same file
        if (File.Exists(Full(CaseUpper))) return;
        Touch(CaseUpper);
        Created |= FixtureFeatures.CaseCollision;
    }

    static List<string> Filler(int seed, int count)
    {
        string[] words = ["alpha", "build", "cache", "delta", "event", "frame", "graph", "index", "layer", "model", "parse", "query", "route", "store", "token", "video"];
        string[] exts = [".cs", ".json", ".md", ".txt", ".png", ".log", ".xml", ".ts"];
        var rng = new Random(seed);
        var files = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            var dir = $"filler/{words[rng.Next(words.Length)]}-{rng.Next(8)}/{words[rng.Next(words.Length)]}";
            files.Add($"{dir}/{words[rng.Next(words.Length)]}_{i:D4}{exts[rng.Next(exts.Length)]}");
        }
        return files;
    }

    public void Dispose()
    {
        try
        {
            Unlock();
            // remove links first so a recursive delete can't wander through them
            foreach (var link in (string[])[JunctionDir, LoopDir])
                if (Directory.Exists(Full(link))) Directory.Delete(Full(link));
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // best effort, it's a temp dir
        }
    }
}

[Flags]
public enum FixtureFeatures
{
    None = 0,
    Junction = 1,
    SymlinkLoop = 2,
    LongPath = 4,
    Unreadable = 8,
    CaseCollision = 16,
}
