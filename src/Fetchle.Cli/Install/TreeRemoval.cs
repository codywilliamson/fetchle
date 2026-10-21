using System.Diagnostics;
using Fetchle.Core.Index;
using Fetchle.Core.Walking;

namespace Fetchle.Cli.Install;

// Locked: our files still there. Kept: our dirs left because they hold something we didn't make
internal sealed record RemovalResult(List<string> Locked, bool CleanupSpawned, List<string> Kept);

// deletes the files fetchle itself puts in its install and data dirs, then each dir only if that left it empty.
// nothing is deleted recursively, so a receipt pointing at a dir someone else uses costs them nothing.
// windows can't delete a running exe, so whatever is still there afterwards is handed to a detached cmd
// that retries until this process has exited
internal static class TreeRemoval
{
    const string EMPTY_PARENT_NAME = "fetchle";

    // every name the install scripts, setup, roots and the index write
    static readonly string[] InstallFiles = ["fetchle.exe", "fetchle", "fetchle.exe.old", "fetchle.old", "fetchle.new", "fetchle.exe.new",
        "potion-retrieval-32M.fmodel", "THIRD-PARTY-NOTICES.md"];
    const string RECEIPT_FILE = "receipt.json";
    static readonly string[] DataFiles = [RECEIPT_FILE, RECEIPT_FILE + ".tmp", "config.json", "config.json.tmp"];
    static readonly string[] IndexFiles = [IndexStore.SNAPSHOT_FILE, IndexStore.SNAPSHOT_FILE + IndexStore.TEMP_SUFFIX, IndexStore.LOCK_FILE];
    const string INDEX_DIR = "index";

    public static RemovalResult Remove(string installDir, string dataDir)
    {
        var owned = OwnedPaths(installDir, dataDir);
        foreach (var file in owned.Files)
        {
            if (!IsReceipt(file, dataDir))
            {
                TryDeleteFile(file);
            }
        }

        // the receipt is the plan for a retry, so it goes only once everything else it lists is gone
        if (!owned.Files.Exists(f => !IsReceipt(f, dataDir) && File.Exists(f)))
        {
            TryDeleteFile(Path.Combine(dataDir, RECEIPT_FILE));
        }

        foreach (var dir in owned.Dirs)
        {
            TryDeleteEmpty(dir);
        }

        var locked = owned.Files.Where(File.Exists).ToList();
        if (locked.Count == 0)
        {
            return new RemovalResult([], false, owned.Dirs.Where(Directory.Exists).ToList());
        }

        if (!OperatingSystem.IsWindows())
        {
            return new RemovalResult(locked, false, []);
        }

        var receipt = Path.Combine(dataDir, RECEIPT_FILE);
        SpawnCleanup(CleanupScript.Build([.. locked.Where(f => !IsReceipt(f, dataDir))], File.Exists(receipt) ? receipt : null, owned.Dirs));
        return new RemovalResult(locked, true, []);
    }

    // files first, then dirs innermost first, then empty parents named fetchle (only there because we made them)
    internal static (List<string> Files, List<string> Dirs) OwnedPaths(string installDir, string dataDir)
    {
        var index = Path.Combine(dataDir, INDEX_DIR);
        var files = InstallFiles.Select(f => Path.Combine(installDir, f))
            .Concat(DataFiles.Select(f => Path.Combine(dataDir, f)))
            .Concat(IndexFiles.Select(f => Path.Combine(index, f)))
            .ToList();
        var dirs = new List<string> { index, installDir, dataDir };
        foreach (var parent in new[] { installDir, dataDir }.Select(Path.GetDirectoryName).OfType<string>())
        {
            if (Path.GetFileName(parent).Equals(EMPTY_PARENT_NAME, StringComparison.OrdinalIgnoreCase)
                && !dirs.Exists(d => d.Equals(parent, PathComparison.Default)))
            {
                dirs.Add(parent);
            }
        }
        return (files, dirs);
    }

    static bool IsReceipt(string file, string dataDir) => file.Equals(Path.Combine(dataDir, RECEIPT_FILE), PathComparison.Default);

    static void TryDeleteFile(string file)
    {
        try
        {
            if (File.Exists(file))
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // locked, the cleanup pass takes it
        }
    }

    // never recursive: a dir that still holds something we didn't make stays
    static void TryDeleteEmpty(string dir)
    {
        try
        {
            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
            {
                Directory.Delete(dir);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // raced or locked
        }
    }

    static void SpawnCleanup(string script)
    {
        var info = new ProcessStartInfo("cmd.exe", $"/d /s /c \"{script}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetTempPath(),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        // redirected so the child holds none of our stdio handles. a test or script waiting on our output would otherwise wait on it too
        using var process = Process.Start(info);
        process?.StandardInput.Close();
    }
}

internal static class CleanupScript
{
    const int RETRIES = 60;
    const string SLEEP_ONE_SECOND = "ping -n 2 127.0.0.1 >nul";

    // each locked file: retry del for about a minute, which outlasts this process exiting. the receipt goes next,
    // but only if every one of those is gone, so a file still locked after a minute keeps its plan for a retry.
    // then each dir with a plain rmdir, which only removes it if empty. loops are parenthesized or the next one
    // nests inside it. echo is off because for loops echo into our stdout pipe, and cmd dies when that breaks
    public static string Build(IReadOnlyList<string> files, string? receipt, IReadOnlyList<string> dirs)
    {
        var steps = files.Select(file => $"(for /l %n in (1,1,{RETRIES}) do if exist \"{file}\" ({SLEEP_ONE_SECOND} & del /f /q \"{file}\" 2>nul))").ToList();
        if (receipt is not null)
        {
            var guards = string.Concat(files.Select(file => $"if not exist \"{file}\" "));
            steps.Add($"({guards}del /f /q \"{receipt}\" 2>nul)");
        }
        steps.AddRange(dirs.Select(dir => $"rmdir \"{dir}\" 2>nul"));
        return "@echo off & " + string.Join(" & ", steps);
    }
}
