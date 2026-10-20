using Fetchle.Cli.Doctor;
using Fetchle.Cli.Install;
using Fetchle.Core.Index;

namespace Fetchle.Tests.Install;

public class DoctorCheckTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    static readonly string Root = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);

    static SnapshotInfo Snapshot(params string[] roots) =>
        new("snapshot.fidx", Now.AddHours(-2), 1000, 500, 40_000, roots.Select(r => new SnapshotRoot(r, 1000, 1000, TimeSpan.FromMilliseconds(5))).ToList());

    [Test]
    [Arguments(0, 0, "Fail")]
    [Arguments(2, 1, "Fail")]
    [Arguments(1, 1, "Pass")]
    [Arguments(1, 0, "Warn")]
    [Arguments(1, 2, "Warn")]
    public async Task Path_check_by_count(int persisted, int inProcess, string expectedName)
    {
        var expected = Enum.Parse<CheckStatus>(expectedName);
        var result = PathCheck.Evaluate(persisted, inProcess, "/bin");
        await Assert.That(result.Status).IsEqualTo(expected);
        await Assert.That(result.Fix).IsEqualTo(expected == CheckStatus.Fail ? FixCommands.SETUP : null);
    }

    [Test]
    public async Task Version_older_than_latest_warns_with_the_install_command()
    {
        var result = VersionCheck.Evaluate("0.1.0", "v0.2.0", null);
        await Assert.That(result.Status).IsEqualTo(CheckStatus.Warn);
        await Assert.That(result.Fix).IsEqualTo(FixCommands.InstallScript);
    }

    [Test]
    [Arguments("0.2.0", "v0.2.0")]
    [Arguments("0.3.0-rc1", "v0.2.0")]
    public async Task Version_current_or_newer_passes(string current, string latest) =>
        await Assert.That(VersionCheck.Evaluate(current, latest, null).Status).IsEqualTo(CheckStatus.Pass);

    [Test]
    public async Task Unreachable_release_api_warns() =>
        await Assert.That(VersionCheck.Evaluate("0.1.0", null, "unreachable").Status).IsEqualTo(CheckStatus.Warn);

    [Test]
    public async Task Missing_index_with_roots_fails_with_rebuild()
    {
        var results = IndexCheck.Evaluate(() => null, [Root], Now);
        await Assert.That(results[0].Status).IsEqualTo(CheckStatus.Fail);
        await Assert.That(results[0].Fix).IsEqualTo(FixCommands.INDEX_REBUILD);
    }

    [Test]
    public async Task Missing_index_without_roots_only_warns()
    {
        var results = IndexCheck.Evaluate(() => null, [], Now);
        await Assert.That(results[0].Status).IsEqualTo(CheckStatus.Warn);
    }

    [Test]
    public async Task Unreadable_index_fails_with_rebuild()
    {
        var results = IndexCheck.Evaluate(() => throw new IndexUnreadableException("x/snapshot.fidx", "truncated"), [Root], Now);
        await Assert.That(results[0].Status).IsEqualTo(CheckStatus.Fail);
        await Assert.That(results[0].Detail).Contains("truncated");
        await Assert.That(results[0].Fix).IsEqualTo(FixCommands.INDEX_REBUILD);
    }

    [Test]
    public async Task Healthy_index_reports_age_size_and_bytes_per_entry()
    {
        var results = IndexCheck.Evaluate(() => Snapshot(Root), [Root], Now);
        await Assert.That(results[0].Status).IsEqualTo(CheckStatus.Pass);
        await Assert.That(results[0].Detail).Contains("40.0 bytes per entry");
        await Assert.That(results[1].Status).IsEqualTo(CheckStatus.Pass);
    }

    [Test]
    public async Task A_configured_root_missing_from_the_snapshot_fails_with_rebuild()
    {
        var results = IndexCheck.Evaluate(() => Snapshot(), [Root], Now);
        await Assert.That(results[1].Status).IsEqualTo(CheckStatus.Fail);
        await Assert.That(results[1].Fix).IsEqualTo(FixCommands.INDEX_REBUILD);
    }

    [Test]
    public async Task A_root_that_no_longer_exists_fails_with_remove()
    {
        var gone = Path.Combine(Root, $"fetchle-gone-{Guid.NewGuid():N}");
        var results = IndexCheck.Evaluate(() => Snapshot(gone), [gone], Now);
        await Assert.That(results[1].Status).IsEqualTo(CheckStatus.Fail);
        await Assert.That(results[1].Fix).IsEqualTo($"fetchle roots remove {gone}");
    }

    [Test]
    public async Task Receipt_checks()
    {
        var receipt = new Receipt("0.1.0", Channels.SCRIPT, "/b", "/d", null, []);
        await Assert.That(ReceiptChecks.Receipt(receipt, null, "/r").Status).IsEqualTo(CheckStatus.Pass);
        await Assert.That(ReceiptChecks.Receipt(null, null, "/r").Status).IsEqualTo(CheckStatus.Warn);
        await Assert.That(ReceiptChecks.Receipt(null, "corrupt", "/r").Status).IsEqualTo(CheckStatus.Fail);
    }

    [Test]
    public async Task Agent_checks()
    {
        var empty = new Receipt("0.1.0", Channels.SCRIPT, "/b", "/d", null, []);
        var withAgent = empty with { Agents = [new AgentRegistration("claude-code", "mcp", "/c.json")] };
        await Assert.That(ReceiptChecks.Agents(empty, _ => true).Status).IsEqualTo(CheckStatus.Skip);
        await Assert.That(ReceiptChecks.Agents(withAgent, _ => true).Status).IsEqualTo(CheckStatus.Pass);
        await Assert.That(ReceiptChecks.Agents(withAgent, _ => false).Status).IsEqualTo(CheckStatus.Fail);
    }

    [Test]
    public async Task Inotify_checks()
    {
        await Assert.That(HostChecks.Inotify(null).Status).IsEqualTo(CheckStatus.Skip);
        await Assert.That(HostChecks.Inotify(8192).Status).IsEqualTo(CheckStatus.Warn);
        await Assert.That(HostChecks.Inotify(524_288).Status).IsEqualTo(CheckStatus.Pass);
    }
}
