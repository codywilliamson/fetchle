using Fetchle.Cli.Install;

namespace Fetchle.Tests.Install;

public class ReceiptTests
{
    static string TempFile() => Path.Combine(Path.GetTempPath(), $"fetchle-receipt-{Guid.NewGuid():N}", "receipt.json");

    static Receipt Sample() => new(
        "0.1.0",
        Channels.SCRIPT,
        @"C:\Users\me\AppData\Local\fetchle\bin",
        @"C:\Users\me\AppData\Local\fetchle",
        new PathEdit(PathEditKinds.WINDOWS_USER_PATH, @"C:\Users\me\AppData\Local\fetchle\bin"),
        []);

    // records compare lists by reference, so check the parts
    static async Task AssertSame(Receipt? actual, Receipt expected)
    {
        await Assert.That(actual).IsNotNull();
        var none = new List<AgentRegistration>();
        await Assert.That(actual! with { Agents = none }).IsEqualTo(expected with { Agents = none });
        await Assert.That(actual.Agents).IsEquivalentTo(expected.Agents);
    }

    [Test]
    public async Task Round_trips_through_disk()
    {
        var path = TempFile();
        try
        {
            ReceiptStore.Save(path, Sample());
            await AssertSame(ReceiptStore.Load(path), Sample());
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Test]
    public async Task Writes_snake_case_keys_and_an_empty_agents_array()
    {
        var path = TempFile();
        try
        {
            ReceiptStore.Save(path, Sample());
            var json = File.ReadAllText(path);
            await Assert.That(json).Contains("\"install_dir\"");
            await Assert.That(json).Contains("\"path_edit\"");
            await Assert.That(json).Contains("\"agents\": []");
            await Assert.That(json).DoesNotContain("\"file\"");
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Test]
    public async Task A_shell_edit_keeps_its_rc_file()
    {
        var path = TempFile();
        try
        {
            var receipt = Sample() with { PathEdit = new PathEdit(PathEditKinds.SHELL_RC_BLOCK, "/home/me/.local/share/fetchle/bin", "/home/me/.bashrc") };
            ReceiptStore.Save(path, receipt);
            await AssertSame(ReceiptStore.Load(path), receipt);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Test]
    public async Task Missing_file_loads_as_null() =>
        await Assert.That(ReceiptStore.Load(TempFile())).IsNull();

    [Test]
    public async Task Corrupt_file_throws_invalid_data()
    {
        var path = TempFile();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ nope");
        try
        {
            await Assert.That(() => ReceiptStore.Load(path)).Throws<InvalidDataException>();
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }
}
