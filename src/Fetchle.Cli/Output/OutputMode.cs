namespace Fetchle.Cli.Output;

public enum OutputMode { Pretty, Plain, Agent, Json }

static class OutputModes
{
    static readonly string[] AgentEnvVars = ["CLAUDECODE"];

    public static OutputMode Detect(bool json, bool plain, bool stdoutRedirected, Func<string, string?> getEnv)
    {
        if (json)
        {
            return OutputMode.Json;
        }

        if (plain)
        {
            return OutputMode.Plain;
        }

        foreach (var name in AgentEnvVars)
        {
            if (!string.IsNullOrEmpty(getEnv(name)))
            {
                return OutputMode.Agent;
            }
        }

        return stdoutRedirected ? OutputMode.Plain : OutputMode.Pretty;
    }

    public static bool AllowsLiveWidgets(OutputMode mode) => mode == OutputMode.Pretty;
}
