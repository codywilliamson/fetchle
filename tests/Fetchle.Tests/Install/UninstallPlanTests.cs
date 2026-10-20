using Fetchle.Cli.Install;

namespace Fetchle.Tests.Install;

public class UninstallPlanTests
{
    static readonly string Home = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "fake-home"));

    static Receipt WithDirs(string install, string data) => new("0.1.0", Channels.SCRIPT, install, data, null, []);

    [Test]
    public async Task Ordinary_dirs_under_home_are_fine()
    {
        var plan = UninstallPlan.From(WithDirs(Path.Combine(Home, "f", "bin"), Path.Combine(Home, "f")), Home);
        await Assert.That(plan.Problem).IsNull();
    }

    [Test]
    public async Task The_home_directory_is_refused() =>
        await Assert.That(UninstallPlan.From(WithDirs(Path.Combine(Home, "f", "bin"), Home), Home).Problem).IsNotNull();

    [Test]
    public async Task A_filesystem_root_is_refused() =>
        await Assert.That(UninstallPlan.From(WithDirs(Path.GetPathRoot(Home)!, Path.Combine(Home, "f")), Home).Problem).IsNotNull();

    [Test]
    public async Task A_relative_path_is_refused() =>
        await Assert.That(UninstallPlan.From(WithDirs("bin", Path.Combine(Home, "f")), Home).Problem).IsNotNull();

    [Test]
    public async Task Package_manager_channels_map_to_their_command()
    {
        await Assert.That(ChannelCommands.UninstallCommand("brew")).IsEqualTo("brew uninstall fetchle");
        await Assert.That(ChannelCommands.UninstallCommand("dotnet-tool")).IsEqualTo("dotnet tool uninstall -g fetchle");
        await Assert.That(ChannelCommands.UninstallCommand(Channels.SCRIPT)).IsNull();
    }

    [Test]
    public async Task Cleanup_script_retries_each_file_then_removes_only_empty_dirs()
    {
        var script = CleanupScript.Build([@"C:inetchle.exe"], [@"C:in", @"C:"]);
        await Assert.That(script).StartsWith("@echo off");
        await Assert.That(script).Contains("if exist \"C:\f\bin\fetchle.exe\"");
        await Assert.That(script).Contains("del /f /q \"C:\f\bin\fetchle.exe\"");
        await Assert.That(script).DoesNotContain("/s");
        await Assert.That(script).EndsWith("rmdir \"C:\f\" 2>nul");
    }

    [Test]
    public async Task Removal_keeps_files_fetchle_did_not_create()
    {
        var root = Path.Combine(Path.GetTempPath(), $"fetchle-removal-{Guid.NewGuid():N}");
        var install = Path.Combine(root, "tools");
        var data = Path.Combine(root, "work");
        Directory.CreateDirectory(Path.Combine(data, "index"));
        Directory.CreateDirectory(install);
        File.WriteAllText(Path.Combine(install, OperatingSystem.IsWindows() ? "fetchle.exe" : "fetchle"), "");
        File.WriteAllText(Path.Combine(install, "other-tool"), "");
        File.WriteAllText(Path.Combine(data, "receipt.json"), "");
        File.WriteAllText(Path.Combine(data, "index", "snapshot.fidx"), "");
        File.WriteAllText(Path.Combine(data, "notes.txt"), "mine");

        var result = TreeRemoval.Remove(install, data);

        await Assert.That(result.Locked).IsEmpty();
        await Assert.That(File.Exists(Path.Combine(install, "other-tool"))).IsTrue();
        await Assert.That(File.ReadAllText(Path.Combine(data, "notes.txt"))).IsEqualTo("mine");
        await Assert.That(File.Exists(Path.Combine(data, "receipt.json"))).IsFalse();
        await Assert.That(Directory.Exists(Path.Combine(data, "index"))).IsFalse();
        await Assert.That(result.Kept).IsEquivalentTo([install, data]);
        Directory.Delete(root, recursive: true);
    }

    [Test]
    public async Task A_dir_above_home_is_refused()
    {
        var parent = Path.GetDirectoryName(Home)!;
        await Assert.That(UninstallPlan.From(WithDirs(Path.Combine(Home, "f", "bin"), parent), Home).Problem).IsNotNull();
        if (!OperatingSystem.IsLinux())
        {
            await Assert.That(UninstallPlan.From(WithDirs(Path.Combine(Home, "f", "bin"), Home.ToUpperInvariant()), Home).Problem).IsNotNull();
        }
    }
}
