using Fetchle.Cli.Install;

namespace Fetchle.Tests.Install;

public class ShellRcBlockTests
{
    [Test]
    public async Task Add_and_remove_keep_the_users_lines_and_leave_no_temp_file()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"fetchle-rc-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var rc = Path.Combine(dir, ".bashrc");
        File.WriteAllText(rc, "alias ll='ls -l'\n");
        var block = new ShellRcBlock(rc);
        try
        {
            block.Add("/opt/fetchle/bin");
            await Assert.That(File.ReadAllText(rc)).StartsWith("alias ll='ls -l'\n");
            await Assert.That(block.Count("/opt/fetchle/bin")).IsEqualTo(1);

            block.Remove("/opt/fetchle/bin");
            await Assert.That(File.ReadAllText(rc).Trim()).IsEqualTo("alias ll='ls -l'");
            await Assert.That(Directory.GetFiles(dir)).IsEquivalentTo([rc]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task A_symlinked_rc_stays_a_symlink()
    {
        if (OperatingSystem.IsWindows())
        {
            Skip.Test("symlinks need a privilege on windows; rc files are a unix thing anyway");
        }

        var dir = Path.Combine(Path.GetTempPath(), $"fetchle-rc-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var real = Path.Combine(dir, "dotfiles-bashrc");
        var rc = Path.Combine(dir, ".bashrc");
        File.WriteAllText(real, "export A=1\n");
        File.CreateSymbolicLink(rc, real);
        try
        {
            new ShellRcBlock(rc).Add("/opt/fetchle/bin");
            await Assert.That(new FileInfo(rc).LinkTarget).IsEqualTo(real);
            await Assert.That(File.ReadAllText(real)).Contains("/opt/fetchle/bin");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
