using Fetchle.Cli.Install;

namespace Fetchle.Cli.Doctor;

internal static class ReceiptChecks
{
    const string RECEIPT_NAME = "receipt";
    const string AGENTS_NAME = "agents";

    // receipt is null when missing, error is set when it exists but can't be read
    public static CheckResult Receipt(Receipt? receipt, string? error, string path)
    {
        if (error is not null)
        {
            return new CheckResult(RECEIPT_NAME, CheckStatus.Fail, error, FixCommands.SETUP);
        }

        if (receipt is null)
        {
            return new CheckResult(RECEIPT_NAME, CheckStatus.Warn, $"no receipt at {path}, uninstall can't run without it", FixCommands.SETUP);
        }

        return new CheckResult(RECEIPT_NAME, CheckStatus.Pass, $"{receipt.Channel} install, version {receipt.Version}");
    }

    public static CheckResult Agents(Receipt? receipt, Func<string, bool> exists)
    {
        if (receipt is null)
        {
            return new CheckResult(AGENTS_NAME, CheckStatus.Skip, "no receipt");
        }

        if (receipt.Agents.Count == 0)
        {
            return new CheckResult(AGENTS_NAME, CheckStatus.Skip, "none registered, agent setup arrives in v0.3");
        }

        var missing = receipt.Agents.Where(agent => !exists(agent.File)).Select(agent => $"{agent.Agent} {agent.Kind} ({agent.File})").ToList();
        if (missing.Count > 0)
        {
            return new CheckResult(AGENTS_NAME, CheckStatus.Fail, $"missing: {string.Join(", ", missing)}", FixCommands.SETUP);
        }

        return new CheckResult(AGENTS_NAME, CheckStatus.Pass, $"{receipt.Agents.Count} registration(s) in place");
    }
}
