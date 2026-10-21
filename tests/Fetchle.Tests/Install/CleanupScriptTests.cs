using System.Diagnostics;
using Fetchle.Cli.Install;

namespace Fetchle.Tests.Install;

// the script goes to cmd on its command line, where %name% expands even inside double quotes
public class CleanupScriptTests
{
    [Test]
    public async Task A_percent_in_a_path_is_escaped_so_cmd_cannot_expand_it()
    {
        var script = CleanupScript.Build([@"C:\f\100%n\fetchle.exe"], @"C:\f\%TEMP%\receipt.json", [@"C:\f\%TEMP%"]);

        await Assert.That(script).DoesNotContain("100%n");
        await Assert.That(script).DoesNotContain("%TEMP%");
        await Assert.That(script).Contains(@"""C:\f\100""^%""n\fetchle.exe""");
        await Assert.That(script).Contains(@"rmdir ""C:\f\""^%""TEMP""^%""""");
    }

    [Test]
    public async Task A_path_without_a_percent_is_left_alone()
    {
        var script = CleanupScript.Build([@"C:\f\bin\fetchle.exe"], null, [@"C:\f"]);

        await Assert.That(script).DoesNotContain("^");
    }

    // %OS% is set on every windows box, so an unescaped name like this one would be rewritten to a file that doesn't exist
    [Test]
    public async Task Cmd_deletes_a_file_whose_name_looks_like_a_variable()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("the cleanup script is a windows cmd script");
        }

        var dir = Path.Combine(Path.GetTempPath(), $"fetchle-cleanup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "x%OS%y.exe");
        File.WriteAllText(file, "");
        try
        {
            var script = CleanupScript.Build([file], null, [dir]);
            using var cmd = Process.Start(new ProcessStartInfo("cmd.exe", $"/d /s /c \"{script}\"") { UseShellExecute = false, CreateNoWindow = true })!;
            await cmd.WaitForExitAsync();

            await Assert.That(File.Exists(file)).IsFalse();
            await Assert.That(Directory.Exists(dir)).IsFalse();
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
