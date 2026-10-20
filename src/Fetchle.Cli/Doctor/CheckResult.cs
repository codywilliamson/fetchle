namespace Fetchle.Cli.Doctor;

internal enum CheckStatus
{
    Pass,
    Warn,
    Fail,
    Skip,
}

// fix is the exact command a person would run. doctor --fix only runs the ones named in FixCommands
internal sealed record CheckResult(string Name, CheckStatus Status, string Detail, string? Fix = null);

internal static class FixCommands
{
    public const string SETUP = "fetchle setup --yes";
    public const string INDEX_REBUILD = "fetchle index rebuild";
    public const string INSTALL_SCRIPT_UNIX = "curl -fsSL https://fetchle.dev/install.sh | sh";
    public const string INSTALL_SCRIPT_WINDOWS = "irm https://fetchle.dev/install.ps1 | iex";

    public static readonly string[] Runnable = [SETUP, INDEX_REBUILD];

    public static string InstallScript => OperatingSystem.IsWindows() ? INSTALL_SCRIPT_WINDOWS : INSTALL_SCRIPT_UNIX;
}
