namespace Fetchle.Cli.Install;

// which rc file gets the PATH block, and what goes in it
internal static class ShellProfile
{
    const string ZSH = "zsh";
    const string BASH = "bash";

    public static string RcFile(string home, string? shell)
    {
        var name = Path.GetFileName((shell ?? "").TrimEnd('/'));
        return name switch
        {
            ZSH => Path.Combine(home, ".zshrc"),
            BASH => Path.Combine(home, ".bashrc"),
            _ => Path.Combine(home, ".profile"),
        };
    }

    // sh-compatible, and a shell that sources the rc file twice still gets one entry
    public static string PathBody(string entry)
    {
        var quoted = DoubleQuoteSafe(entry);
        return $"case \":$PATH:\" in\n  *\":{quoted}:\"*) ;;\n  *) export PATH=\"{quoted}:$PATH\" ;;\nesac";
    }

    static string DoubleQuoteSafe(string text) =>
        text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("$", "\\$").Replace("`", "\\`");
}
