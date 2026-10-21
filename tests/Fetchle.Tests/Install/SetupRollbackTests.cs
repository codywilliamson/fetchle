using Fetchle.Cli.Commands;
using Fetchle.Cli.Install;

namespace Fetchle.Tests.Install;

// setup edits the shell rc first and saves the receipt last. a failed save has to leave the rc as it was
public class SetupRollbackTests
{
    [Test]
    public async Task A_failed_receipt_save_puts_back_the_old_block_in_the_same_rc_file()
    {
        using var sandbox = new Sandbox();
        var oldBin = Path.Combine(sandbox.Root, "old-bin");
        File.WriteAllText(sandbox.Rc, "alias ll='ls -l'\n");
        var rc = new ShellRcBlock(sandbox.Rc);
        var oldEdit = rc.Add(oldBin);
        var before = File.ReadAllText(sandbox.Rc);
        ReceiptStore.Save(sandbox.Receipt, new Receipt("0.0.0", Channels.SCRIPT, oldBin, sandbox.Data, [], oldEdit));
        sandbox.BreakReceiptSave();

        await Assert.That(() => SetupAction.Run(sandbox.Env, TextWriter.Null, rc)).Throws<UnauthorizedAccessException>();

        await Assert.That(File.ReadAllText(sandbox.Rc)).IsEqualTo(before);
    }

    [Test]
    public async Task A_failed_receipt_save_on_a_first_setup_leaves_the_rc_untouched()
    {
        using var sandbox = new Sandbox();
        File.WriteAllText(sandbox.Rc, "alias ll='ls -l'\n");
        sandbox.BreakReceiptSave();

        await Assert.That(() => SetupAction.Run(sandbox.Env, TextWriter.Null, new ShellRcBlock(sandbox.Rc))).Throws<UnauthorizedAccessException>();

        await Assert.That(File.ReadAllText(sandbox.Rc).Trim()).IsEqualTo("alias ll='ls -l'");
    }

    sealed class Sandbox : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), $"fetchle-setup-{Guid.NewGuid():N}");

        public string Data => Path.Combine(Root, "data");

        public string Receipt => Path.Combine(Data, "receipt.json");

        public string Rc => Path.Combine(Root, ".bashrc");

        public CliEnvironment Env => new(Root, true, name => name switch
        {
            "FETCHLE_DATA_DIR" => Data,
            "FETCHLE_INSTALL_DIR" => Path.Combine(Root, "new-bin"),
            "HOME" or "USERPROFILE" => Root,
            _ => null,
        }, TimeProvider.System, "0.0.0-test");

        public Sandbox()
        {
            Directory.CreateDirectory(Data);
        }

        // the receipt is written through receipt.json.tmp, so a directory there fails the save with an access error
        public void BreakReceiptSave() => Directory.CreateDirectory(Receipt + ".tmp");

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
