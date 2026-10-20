using Fetchle.Cli.Install;

namespace Fetchle.Tests.Install;

public class ShellProfileTests
{
    [Test]
    [Arguments("/bin/zsh", ".zshrc")]
    [Arguments("/usr/bin/zsh", ".zshrc")]
    [Arguments("/bin/bash", ".bashrc")]
    [Arguments("/usr/bin/fish", ".profile")]
    [Arguments("", ".profile")]
    [Arguments(null, ".profile")]
    public async Task Picks_the_rc_file_from_shell(string? shell, string expected) =>
        await Assert.That(ShellProfile.RcFile("/home/me", shell)).IsEqualTo(Path.Combine("/home/me", expected));

    [Test]
    public async Task Path_body_guards_against_double_entries()
    {
        var body = ShellProfile.PathBody("/home/me/.local/share/fetchle/bin");
        await Assert.That(body).Contains("case \":$PATH:\" in");
        await Assert.That(body).Contains("export PATH=\"/home/me/.local/share/fetchle/bin:$PATH\"");
    }

    [Test]
    public async Task Path_body_escapes_shell_metacharacters()
    {
        var body = ShellProfile.PathBody("/home/$me/a\"b");
        await Assert.That(body).Contains("/home/\\$me/a\\\"b");
    }
}
