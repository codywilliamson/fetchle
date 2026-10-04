using Fetchle.Cli;

namespace Fetchle.Tests;

public class OutputModeTests
{
    static string? NoEnv(string _) => null;
    static string? ClaudeCode(string name) => name == "CLAUDECODE" ? "1" : null;

    [Test]
    public async Task Tty_is_pretty() =>
        await Assert.That(OutputModes.Detect(json: false, plain: false, stdoutRedirected: false, NoEnv)).IsEqualTo(OutputMode.Pretty);

    [Test]
    public async Task Redirected_is_plain() =>
        await Assert.That(OutputModes.Detect(json: false, plain: false, stdoutRedirected: true, NoEnv)).IsEqualTo(OutputMode.Plain);

    [Test]
    public async Task Plain_flag_forces_plain_on_a_tty() =>
        await Assert.That(OutputModes.Detect(json: false, plain: true, stdoutRedirected: false, NoEnv)).IsEqualTo(OutputMode.Plain);

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Agent_env_is_agent_whether_or_not_redirected(bool redirected) =>
        await Assert.That(OutputModes.Detect(json: false, plain: false, redirected, ClaudeCode)).IsEqualTo(OutputMode.Agent);

    [Test]
    public async Task Empty_agent_env_is_not_agent() =>
        await Assert.That(OutputModes.Detect(json: false, plain: false, stdoutRedirected: true, _ => "")).IsEqualTo(OutputMode.Plain);

    [Test]
    public async Task Json_flag_wins_over_everything() =>
        await Assert.That(OutputModes.Detect(json: true, plain: true, stdoutRedirected: true, ClaudeCode)).IsEqualTo(OutputMode.Json);

    [Test]
    public async Task Plain_flag_wins_over_agent_env() =>
        await Assert.That(OutputModes.Detect(json: false, plain: true, stdoutRedirected: false, ClaudeCode)).IsEqualTo(OutputMode.Plain);

    [Test]
    [Arguments(OutputMode.Pretty, true)]
    [Arguments(OutputMode.Plain, false)]
    [Arguments(OutputMode.Agent, false)]
    [Arguments(OutputMode.Json, false)]
    public async Task Only_pretty_allows_live_widgets(OutputMode mode, bool expected) =>
        await Assert.That(OutputModes.AllowsLiveWidgets(mode)).IsEqualTo(expected);
}
