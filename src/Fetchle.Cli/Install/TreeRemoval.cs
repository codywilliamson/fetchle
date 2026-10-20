using System.Diagnostics;

namespace Fetchle.Cli.Install;

internal sealed record RemovalResult(List<string> Remaining, bool CleanupSpawned);

// deletes directory trees, including the one holding the running exe. windows can't delete a running exe,
// so whatever is still there afterwards is handed to a detached cmd that retries until this process has exited
internal static class TreeRemoval
{
    const string EMPTY_PARENT_NAME = "fetchle";

    public static RemovalResult Remove(IReadOnlyList<string> dirs)
    {
        foreach (var dir in dirs)
        {
            DeleteBestEffort(dir);
        }

        var remaining = dirs.Where(Directory.Exists).ToList();
        var parents = EmptyParents(dirs);
        if (remaining.Count == 0)
        {
            foreach (var parent in parents)
            {
                TryDeleteEmpty(parent);
            }

            return new RemovalResult([], false);
        }

        if (!OperatingSystem.IsWindows())
        {
            return new RemovalResult(remaining, false);
        }

        SpawnCleanup(CleanupScript.Build(remaining, parents));
        return new RemovalResult(remaining, true);
    }

    // a parent called fetchle only exists because we made it, so it goes when it's empty
    static List<string> EmptyParents(IReadOnlyList<string> dirs) => dirs
        .Select(Path.GetDirectoryName)
        .OfType<string>()
        .Where(parent => Path.GetFileName(parent).Equals(EMPTY_PARENT_NAME, StringComparison.OrdinalIgnoreCase))
        .Distinct()
        .ToList();

    static void DeleteBestEffort(string dir)
    {
        if (!Directory.Exists(dir))
        {
            return;
        }

        // a link is removed without following it
        if (new DirectoryInfo(dir).Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            TryDeleteEmpty(dir);
            return;
        }

        foreach (var file in Directory.EnumerateFiles(dir))
        {
            try
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // locked, the cleanup pass takes it
            }
        }

        foreach (var child in Directory.EnumerateDirectories(dir))
        {
            DeleteBestEffort(child);
        }

        TryDeleteEmpty(dir);
    }

    static void TryDeleteEmpty(string dir)
    {
        try
        {
            Directory.Delete(dir);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // not empty or locked
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

    // each dir: retry rmdir for about a minute, which outlasts this process exiting. each loop is parenthesized or the next one nests inside it. echo is off because for loops echo into our stdout pipe, and cmd dies when that breaks. emptied parents go last
    public static string Build(IReadOnlyList<string> dirs, IReadOnlyList<string> emptyParents)
    {
        var steps = dirs.Select(dir => $"(for /l %n in (1,1,{RETRIES}) do if exist \"{dir}\" ({SLEEP_ONE_SECOND} & rmdir /s /q \"{dir}\" 2>nul))").ToList();
        steps.AddRange(emptyParents.Select(parent => $"rmdir \"{parent}\" 2>nul"));
        return "@echo off & " + string.Join(" & ", steps);
    }
}
