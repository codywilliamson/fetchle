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
    public async Task Cleanup_script_retries_each_dir_then_drops_empty_parents()
    {
        var script = CleanupScript.Build([@"C:\f\bin", @"C:\f"], [@"C:\f"]);
        await Assert.That(script).StartsWith("@echo off");
        await Assert.That(script).Contains("if exist \"C:\\f\\bin\"");
        await Assert.That(script).Contains("rmdir /s /q \"C:\\f\\bin\"");
        await Assert.That(script).EndsWith("rmdir \"C:\\f\" 2>nul");
    }
}
