using System.Text.Json;
using Fetchle.E2E.Harness;
using Fetchle.Fixtures;

namespace Fetchle.E2E.Cli;

public class CliOutputTests
{
    const byte Escape = 0x1b;

    [Test]
    public async Task Plain_prints_one_path_per_line_and_no_escapes()
    {
        using var tree = FixtureTree.Create();
        var run = await FetchleProcess.RunAsync(["settings", "--root", tree.Root]);

        await Assert.That(run.ExitCode).IsEqualTo(0);
        await Assert.That(run.Lines).IsEquivalentTo([tree.Full(FixtureTree.SettingsFile)]);
        await Assert.That(run.StdoutBytes.Contains(Escape)).IsFalse();
    }

    [Test]
    public async Task Agent_adds_one_footer_line()
    {
        using var tree = FixtureTree.Create();
        var run = await FetchleProcess.RunAsync(["settings", "--root", tree.Root], agent: true);

        await Assert.That(run.ExitCode).IsEqualTo(0);
        await Assert.That(run.Lines.Length).IsEqualTo(2);
        await Assert.That(run.Lines[0]).IsEqualTo(tree.Full(FixtureTree.SettingsFile));
        await Assert.That(run.Lines[1]).Matches(@"^1 matches, showing 1, \d+ms$");
        await Assert.That(run.StdoutBytes.Contains(Escape)).IsFalse();
    }

    [Test]
    public async Task Json_prints_hits_then_a_summary()
    {
        using var tree = FixtureTree.Create();
        var run = await FetchleProcess.RunAsync(["settings", "--root", tree.Root, "--json"], agent: true);

        await Assert.That(run.ExitCode).IsEqualTo(0);
        await Assert.That(run.Lines.Length).IsEqualTo(2);
        using var hit = JsonDocument.Parse(run.Lines[0]);
        await Assert.That(hit.RootElement.GetProperty("path").GetString()).IsEqualTo(tree.Full(FixtureTree.SettingsFile));
        await Assert.That(hit.RootElement.GetProperty("score").GetDouble()).IsGreaterThan(0);
        await Assert.That(hit.RootElement.GetProperty("size").GetInt64()).IsGreaterThan(0);
        await Assert.That(hit.RootElement.GetProperty("modified").GetDateTimeOffset()).IsLessThanOrEqualTo(DateTimeOffset.UtcNow);
        using var summary = JsonDocument.Parse(run.Lines[1]);
        await Assert.That(summary.RootElement.GetProperty("total").GetInt32()).IsEqualTo(1);
        await Assert.That(summary.RootElement.GetProperty("shown").GetInt32()).IsEqualTo(1);
        await Assert.That(summary.RootElement.GetProperty("elapsed_ms").GetInt64()).IsGreaterThanOrEqualTo(0);
        await Assert.That(summary.RootElement.GetProperty("stopped_early").ValueKind).IsEqualTo(JsonValueKind.Null);
    }

    [Test]
    public async Task Junctions_and_loops_are_not_followed()
    {
        using var tree = FixtureTree.Create();
        await Assert.That(tree.Created.HasFlag(FixtureFeatures.Junction | FixtureFeatures.SymlinkLoop)).IsTrue();
        var run = await FetchleProcess.RunAsync(["main.cs", "--root", tree.Root]);

        await Assert.That(run.ExitCode).IsEqualTo(0);
        await Assert.That(run.Lines).IsEquivalentTo([tree.Full("src/app/main.cs")]);
    }

    [Test]
    public async Task Unicode_names_round_trip()
    {
        using var tree = FixtureTree.Create();
        foreach (var file in FixtureTree.UnicodeFiles)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var run = await FetchleProcess.RunAsync([name, "--root", tree.Root]);
            await Assert.That(run.Lines).IsEquivalentTo([tree.Full(file)]);
        }
    }

    [Test]
    public async Task Paths_over_260_chars_are_found()
    {
        using var tree = FixtureTree.Create();
        var run = await FetchleProcess.RunAsync([FixtureTree.LongPathMarker, "--root", tree.Root]);

        await Assert.That(run.Lines).IsEquivalentTo([tree.Full(tree.LongPathFile)]);
        await Assert.That(run.Lines[0].Length).IsGreaterThan(260);
    }

    [Test]
    public async Task Unreadable_directories_are_skipped_silently()
    {
        using var tree = FixtureTree.Create();
        Skip.Unless(tree.Created.HasFlag(FixtureFeatures.Unreadable), "this user can read everything");
        var run = await FetchleProcess.RunAsync(["secret-locked", "--root", tree.Root]);

        await Assert.That(run.ExitCode).IsEqualTo(1);
        await Assert.That(run.Stdout).IsEmpty();
        await Assert.That(run.Stderr).IsEmpty();
    }

    [Test]
    public async Task Case_collisions_return_both_files()
    {
        using var tree = FixtureTree.Create();
        Skip.Unless(tree.Created.HasFlag(FixtureFeatures.CaseCollision), "case-insensitive file system");
        var run = await FetchleProcess.RunAsync(["readme-collide", "--root", tree.Root]);

        await Assert.That(run.Lines).IsEquivalentTo([tree.Full(FixtureTree.CaseLower), tree.Full(FixtureTree.CaseUpper)]);
    }

    [Test]
    public async Task Missing_root_fails_fast_with_usage_error()
    {
        using var tree = FixtureTree.Create();
        var run = await FetchleProcess.RunAsync(["settings", "--root", tree.Full("does-not-exist")]);

        await Assert.That(run.ExitCode).IsEqualTo(2);
        await Assert.That(run.Stderr).Contains("root not found");
        await Assert.That(run.Stdout).IsEmpty();
    }

    [Test]
    public async Task Expired_budget_with_no_results_exits_4()
    {
        using var tree = FixtureTree.Create();
        var run = await FetchleProcess.RunAsync(["settings", "--root", tree.Root, "--budget", "0ms"], agent: true);

        await Assert.That(run.ExitCode).IsEqualTo(4);
        await Assert.That(run.Lines).IsEquivalentTo(["0 matches, showing 0, 0ms, stopped early: budget"]);
    }

    [Test]
    public async Task No_results_exits_1()
    {
        using var tree = FixtureTree.Create();
        var run = await FetchleProcess.RunAsync(["no-such-file-anywhere", "--root", tree.Root]);

        await Assert.That(run.ExitCode).IsEqualTo(1);
        await Assert.That(run.Stdout).IsEmpty();
    }

    [Test]
    public async Task Limit_caps_results_but_not_the_total()
    {
        using var tree = FixtureTree.Create();
        var run = await FetchleProcess.RunAsync([".json", "--root", tree.Root, "--limit", "2", "--type", "f"], agent: true);

        await Assert.That(run.Lines.Length).IsEqualTo(3);
        await Assert.That(run.Lines[2]).Matches(@"^\d+ matches, showing 2, ");
    }
}
