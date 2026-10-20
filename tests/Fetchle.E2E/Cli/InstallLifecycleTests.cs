using System.Text.Json;
using Fetchle.E2E.Harness;

namespace Fetchle.E2E.Cli;

public class InstallLifecycleTests
{
    const string BLOCK_START = "# >>> fetchle >>>";
    static readonly TimeSpan CleanupWait = TimeSpan.FromSeconds(30);

    static int Count(string text, string needle) => text.Split(needle).Length - 1;

    static string? UserPath() => OperatingSystem.IsWindows() ? Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) : null;

    [Test]
    [NotInParallel("user-path")]
    public async Task Setup_and_uninstall_leave_the_machine_as_they_found_it()
    {
        Skip.Unless(InstallSandbox.CanEditUserPath, "setup edits the real user PATH on Windows, runs on CI only");
        using var sandbox = new InstallSandbox();
        var exe = sandbox.InstallExe();
        var userPathBefore = UserPath();

        var first = await sandbox.RunAsync(exe, "setup", "--yes");
        var second = await sandbox.RunAsync(exe, "setup", "--yes");
        try
        {
            await Assert.That(first.ExitCode).IsEqualTo(0);
            await Assert.That(second.ExitCode).IsEqualTo(0);

            using var receipt = JsonDocument.Parse(await File.ReadAllTextAsync(sandbox.Receipt));
            await Assert.That(receipt.RootElement.GetProperty("channel").GetString()).IsEqualTo("script");
            await Assert.That(receipt.RootElement.GetProperty("install_dir").GetString()).IsEqualTo(sandbox.Bin);
            await Assert.That(receipt.RootElement.GetProperty("agents").GetArrayLength()).IsEqualTo(0);

            if (OperatingSystem.IsWindows())
            {
                await Assert.That(Count(";" + UserPath() + ";", ";" + sandbox.Bin + ";")).IsEqualTo(1);
            }
            else
            {
                await Assert.That(Count(await File.ReadAllTextAsync(sandbox.RcFile), BLOCK_START)).IsEqualTo(1);
            }
        }
        finally
        {
            var uninstall = await sandbox.RunAsync(exe, "uninstall", "--yes");
            await Assert.That(uninstall.ExitCode).IsEqualTo(0);
        }

        await Assert.That(await sandbox.WaitUntilGoneAsync(sandbox.Data, CleanupWait)).IsTrue();
        await Assert.That(sandbox.AllFiles()).IsEmpty();
        await Assert.That(UserPath()).IsEqualTo(userPathBefore);
    }

    [Test]
    public async Task Setup_backs_up_a_hand_edited_block_before_replacing_it()
    {
        Skip.Unless(!OperatingSystem.IsWindows(), "the PATH block only exists on Unix");
        using var sandbox = new InstallSandbox();
        var exe = sandbox.InstallExe();
        var edited = $"alias a=b\n{BLOCK_START}\n# my own tweak\n# <<< fetchle <<<\n";
        await File.WriteAllTextAsync(sandbox.RcFile, edited);

        var run = await sandbox.RunAsync(exe, "setup", "--yes");
        await Assert.That(run.ExitCode).IsEqualTo(0);

        await Assert.That(await File.ReadAllTextAsync(sandbox.RcFile + ".bak")).IsEqualTo(edited);
        var rc = await File.ReadAllTextAsync(sandbox.RcFile);
        await Assert.That(Count(rc, BLOCK_START)).IsEqualTo(1);
        await Assert.That(rc).DoesNotContain("my own tweak");
        await Assert.That(rc).StartsWith("alias a=b\n");

        await sandbox.RunAsync(exe, "uninstall", "--yes");
        await Assert.That(await File.ReadAllTextAsync(sandbox.RcFile)).IsEqualTo("alias a=b\n");
    }

    [Test]
    public async Task Uninstall_without_a_receipt_refuses_and_prints_manual_steps()
    {
        using var sandbox = new InstallSandbox();
        var exe = sandbox.InstallExe();

        var run = await sandbox.RunAsync(exe, "uninstall", "--yes");

        await Assert.That(run.ExitCode).IsEqualTo(5);
        await Assert.That(run.Stderr).Contains("no receipt");
        await Assert.That(run.Stderr).Contains("by hand");
        await Assert.That(File.Exists(exe)).IsTrue();
    }

    [Test]
    public async Task Uninstall_defers_to_the_package_manager()
    {
        using var sandbox = new InstallSandbox();
        var exe = sandbox.InstallExe();
        sandbox.WriteReceipt("brew");

        var run = await sandbox.RunAsync(exe, "uninstall", "--yes");

        await Assert.That(run.ExitCode).IsEqualTo(0);
        await Assert.That(run.Stdout).Contains("brew uninstall fetchle");
        await Assert.That(File.Exists(exe)).IsTrue();
    }

    [Test]
    public async Task Uninstall_without_yes_refuses_when_nobody_can_answer()
    {
        using var sandbox = new InstallSandbox();
        var exe = sandbox.InstallExe();
        sandbox.WriteReceipt("script");

        var run = await sandbox.RunAsync(exe, "uninstall");

        await Assert.That(run.ExitCode).IsEqualTo(2);
        await Assert.That(run.Stderr).Contains("--yes");
        await Assert.That(File.Exists(exe)).IsTrue();
    }

    [Test]
    public async Task Uninstall_removes_the_dir_that_holds_its_own_exe()
    {
        using var sandbox = new InstallSandbox();
        var exe = sandbox.InstallExe();
        sandbox.WriteReceipt("script");

        var run = await sandbox.RunAsync(exe, "uninstall", "--yes");

        await Assert.That(run.ExitCode).IsEqualTo(0);
        await Assert.That(await sandbox.WaitUntilGoneAsync(sandbox.Data, CleanupWait)).IsTrue();
        await Assert.That(Directory.Exists(sandbox.Bin)).IsFalse();
    }

    [Test]
    public async Task Uninstall_refuses_a_receipt_that_points_at_the_home_dir()
    {
        using var sandbox = new InstallSandbox();
        var exe = sandbox.InstallExe();
        Directory.CreateDirectory(sandbox.Data);
        await File.WriteAllTextAsync(sandbox.Receipt, $$"""{ "version": "0", "channel": "script", "install_dir": {{JsonSerializer.Serialize(sandbox.Bin)}}, "data_dir": {{JsonSerializer.Serialize(sandbox.Home)}}, "agents": [] }""");

        var run = await sandbox.RunAsync(exe, "uninstall", "--yes");

        await Assert.That(run.ExitCode).IsEqualTo(5);
        await Assert.That(File.Exists(exe)).IsTrue();
    }
}
