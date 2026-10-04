namespace Fetchle.Cli.Output;

public enum OutputMode { Pretty, Plain, Agent, Json }

// docs/specs/cli.md#output-modes
static class OutputModes
{
    // which env vars other agents set is still open, see docs/decisions.md
    static readonly string[] AgentEnvVars = ["CLAUDECODE"];

    public static OutputMode Detect(bool json, bool plain, bool stdoutRedirected, Func<string, string?> getEnv)
    {
        if (json) return OutputMode.Json;
        if (plain) return OutputMode.Plain;
        foreach (var name in AgentEnvVars)
            if (!string.IsNullOrEmpty(getEnv(name))) return OutputMode.Agent;
        return stdoutRedirected ? OutputMode.Plain : OutputMode.Pretty;
    }

    // spinner frames leak into redirected output, see docs/spikes/terminal.md
    public static bool AllowsLiveWidgets(OutputMode mode) => mode == OutputMode.Pretty;
}
