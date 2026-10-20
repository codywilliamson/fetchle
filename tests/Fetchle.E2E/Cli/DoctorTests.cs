using Fetchle.E2E.Harness;

namespace Fetchle.E2E.Cli;

public class DoctorTests
{
    const int FAILED = 5;

    static string MakeTree(InstallSandbox sandbox)
    {
        var tree = Path.Combine(sandbox.Home, "tree");
        Directory.CreateDirectory(Path.Combine(tree, "src"));
        File.WriteAllText(Path.Combine(tree, "src", "settings.json"), "{}");
        return tree;
    }

    [Test]
    public async Task Doctor_before_setup_fails_and_prints_the_fix()
    {
        using var sandbox = new InstallSandbox();
        var exe = sandbox.InstallExe();

        var run = await sandbox.RunAsync(exe, "doctor");

        await Assert.That(run.ExitCode).IsEqualTo(FAILED);
        await Assert.That(run.Stdout).Contains("fail  path");
        await Assert.That(run.Stdout).Contains("fix: fetchle setup --yes");
        await Assert.That(run.Stdout).Contains("skip  watcher");
        await Assert.That(run.Stdout).Contains("not in v0.1");
        await Assert.That(run.Stdout).Contains("pass  terminal");
    }

    [Test]
    [NotInParallel("user-path")]
    public async Task Doctor_fix_round_trips_path_receipt_and_index()
    {
        Skip.Unless(InstallSandbox.CanEditUserPath, "doctor --fix edits the real user PATH on Windows, runs on CI only");
        using var sandbox = new InstallSandbox();
        var exe = sandbox.InstallExe();
        var tree = MakeTree(sandbox);
        var snapshot = Path.Combine(sandbox.Data, "index", "snapshot.fidx");
        try
        {
            await Assert.That((await sandbox.RunAsync(exe, "doctor")).ExitCode).IsEqualTo(FAILED);
            await Assert.That((await sandbox.RunAsync(exe, "doctor", "--fix")).ExitCode).IsEqualTo(0);
            await Assert.That(File.Exists(sandbox.Receipt)).IsTrue();

            await Assert.That((await sandbox.RunAsync(exe, "roots", "add", tree)).ExitCode).IsEqualTo(0);
            await Assert.That((await sandbox.RunAsync(exe, "doctor")).ExitCode).IsEqualTo(FAILED);
            await Assert.That((await sandbox.RunAsync(exe, "doctor", "--fix")).ExitCode).IsEqualTo(0);
            await Assert.That(File.Exists(snapshot)).IsTrue();
            await Assert.That((await sandbox.RunAsync(exe, "doctor")).ExitCode).IsEqualTo(0);

            await File.WriteAllBytesAsync(snapshot, [1, 2, 3]);
            var broken = await sandbox.RunAsync(exe, "doctor");
            await Assert.That(broken.ExitCode).IsEqualTo(FAILED);
            await Assert.That(broken.Stdout).Contains("fix: fetchle index rebuild");
            await Assert.That((await sandbox.RunAsync(exe, "doctor", "--fix")).ExitCode).IsEqualTo(0);
            await Assert.That((await sandbox.RunAsync(exe, "doctor")).ExitCode).IsEqualTo(0);
        }
        finally
        {
            await sandbox.RunAsync(exe, "uninstall", "--yes");
        }
    }
}
