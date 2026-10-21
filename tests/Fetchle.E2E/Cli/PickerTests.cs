using Fetchle.E2E.Harness;

namespace Fetchle.E2E.Cli;

// the e2e harness always redirects stdout, and a ci runner has no tty to hand the exe, so the picker's own ui is covered
// by the unit project on an in-memory terminal. what only the real exe can show is that it refuses to start a widget
// when it has no terminal, and that it says why
public class PickerTests
{
    [Test]
    [Arguments("-i")]
    [Arguments("-i", "settings")]
    [Arguments("-i", "--plain", "settings")]
    [Arguments("-i", "--json", "settings")]
    public async Task Picker_without_a_terminal_exits_2_and_writes_no_frames(params string[] args)
    {
        var run = await FetchleProcess.RunAsync(args);

        await Assert.That(run.ExitCode).IsEqualTo(2);
        await Assert.That(run.Stderr).Contains("the picker needs a terminal");
        await Assert.That(run.Stdout).IsEmpty();
        await Assert.That(run.Stderr).DoesNotContain("\e");
    }

    [Test]
    public async Task Picker_in_agent_mode_exits_2()
    {
        var run = await FetchleProcess.RunAsync(["-i", "settings"], agent: true);

        await Assert.That(run.ExitCode).IsEqualTo(2);
        await Assert.That(run.Stderr).Contains("the picker needs a terminal");
    }
}
