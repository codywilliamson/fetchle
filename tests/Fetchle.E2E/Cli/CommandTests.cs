using Fetchle.E2E.Harness;

namespace Fetchle.E2E.Cli;

public class CommandTests
{
    [Test]
    public async Task Version_prints_a_version()
    {
        var run = await FetchleProcess.RunAsync(["--version"]);
        await Assert.That(run.ExitCode).IsEqualTo(0);
        await Assert.That(run.Stdout.Trim()).Matches(@"^\d+\.\d+\.\d+");
    }

    [Test]
    [Arguments("roots", "list")]
    [Arguments("index", "status")]
    [Arguments("setup")]
    [Arguments("doctor", "--fix")]
    [Arguments("update")]
    [Arguments("cleanup", "--yes")]
    [Arguments("uninstall", "--yes")]
    [Arguments("savings")]
    [Arguments("-i")]
    public async Task Unimplemented_commands_say_so_and_exit_2(params string[] args)
    {
        var run = await FetchleProcess.RunAsync(args);
        await Assert.That(run.ExitCode).IsEqualTo(2);
        await Assert.That(run.Stderr).Contains("not implemented yet");
    }

    [Test]
    [Arguments("--bogus")]
    [Arguments("--budget", "soon", "q")]
    [Arguments("--type", "x", "q")]
    [Arguments("--limit", "0", "q")]
    [Arguments("--limit", "-1", "q")]
    public async Task Bad_args_exit_2(params string[] args) =>
        await Assert.That((await FetchleProcess.RunAsync(args)).ExitCode).IsEqualTo(2);
}
