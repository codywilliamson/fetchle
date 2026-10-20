using Fetchle.Bench;

namespace Fetchle.Bench.Tests;

public class ToolPlansTests
{
    const string FETCHLE = "fetchle";
    // absolute, because the plans expand the root to a full path
    static readonly string ROOT = Path.GetFullPath("corpus-root");
    static readonly IndexDirs Dirs = new("data-query", "data-rebuild", "data-live");

    static readonly LocatedTools Everything = new(FETCHLE, "rg", "fd", "pwsh", "cmd", "find");

    static ToolPlan Plan(LocatedTools tools, string tool) => ToolPlans.For(tools, ROOT, Dirs).Single(p => p.Timed.Tool == tool);

    [Test]
    public async Task Baselines_time_version_flags_of_fetchle_and_rg()
    {
        var plans = ToolPlans.Baselines(Everything);
        await Assert.That(plans.Select(p => p.Timed.Tool)).IsEquivalentTo(["fetchle --version", "rg --version"]);
        await Assert.That(plans.All(p => p.Timed.Arguments.SequenceEqual(["--version"]))).IsTrue();
    }

    [Test]
    public async Task Missing_tools_get_no_plan()
    {
        var tools = new LocatedTools(null, null, null, null, null, null);
        await Assert.That(ToolPlans.For(tools, ROOT, Dirs)).IsEmpty();
    }

    [Test]
    public async Task Every_tool_runs_the_same_query()
    {
        var plans = ToolPlans.For(Everything, ROOT, Dirs);
        var bare = plans.Single(p => p.Timed.Tool == "fetchle bare walk");
        foreach (var plan in plans.Where(p => p != bare && p.Timed.Tool != "fetchle cold rebuild"))
        {
            await Assert.That(plan.Timed.Arguments.Any(a => a.Contains(ToolPlans.QUERY))).IsTrue();
        }
    }

    [Test]
    public async Task Live_walk_passes_the_root_and_a_long_budget()
    {
        var args = Plan(Everything, "fetchle live walk").Timed.Arguments;
        await Assert.That(args).IsEquivalentTo(["settings", "--root", ROOT, "--budget", "5m", "--plain"]);
    }

    [Test]
    public async Task Bare_walk_expects_no_match()
    {
        var command = Plan(Everything, "fetchle bare walk").Timed;
        await Assert.That(command.ExpectedExitCode).IsEqualTo(1);
        await Assert.That(command.Arguments[0]).IsEqualTo(ToolPlans.NO_MATCH_QUERY);
    }

    [Test]
    public async Task Indexed_query_has_no_root_and_an_isolated_data_dir()
    {
        var plan = Plan(Everything, "fetchle indexed query");
        await Assert.That(plan.Timed.Arguments).IsEquivalentTo(["settings", "--budget", "5m", "--plain"]);
        await Assert.That(plan.Timed.Environment!["FETCHLE_DATA_DIR"]).IsEqualTo(Dirs.Query);
    }

    [Test]
    public async Task Indexed_query_sets_up_roots_then_rebuild_in_the_same_data_dir()
    {
        var setup = Plan(Everything, "fetchle indexed query").Setup;
        await Assert.That(setup.Select(s => string.Join(' ', s.Arguments))).IsEquivalentTo([$"roots add {ROOT}", "index rebuild"]);
        await Assert.That(setup.All(s => s.Environment!["FETCHLE_DATA_DIR"] == Dirs.Query)).IsTrue();
    }

    [Test]
    public async Task Cold_rebuild_deletes_its_own_snapshot_before_each_run()
    {
        using var scratch = new TempDir();
        var dirs = new IndexDirs("unused", scratch.Path, "unused-live");
        var plan = ToolPlans.For(Everything, ROOT, dirs).Single(p => p.Timed.Tool == "fetchle cold rebuild");
        var snapshot = Path.Combine(scratch.Path, "index", "snapshot.fidx");
        Directory.CreateDirectory(Path.GetDirectoryName(snapshot)!);
        File.WriteAllText(snapshot, "x");

        plan.Timed.BeforeEachRun!();

        await Assert.That(File.Exists(snapshot)).IsFalse();
        await Assert.That(plan.Timed.Arguments).IsEquivalentTo(["index", "rebuild"]);
        await Assert.That(plan.Timed.Environment!["FETCHLE_DATA_DIR"]).IsEqualTo(scratch.Path);
    }

    [Test]
    public async Task Cold_rebuild_with_no_snapshot_yet_does_not_throw()
    {
        var plan = Plan(Everything, "fetchle cold rebuild");
        await Assert.That(() => plan.Timed.BeforeEachRun!()).ThrowsNothing();
    }

    [Test]
    public async Task Live_walks_get_an_empty_data_dir_of_their_own()
    {
        var plans = ToolPlans.For(Everything, ROOT, Dirs);
        foreach (var tool in (string[])["fetchle live walk", "fetchle bare walk"])
        {
            var command = plans.Single(p => p.Timed.Tool == tool).Timed;
            await Assert.That(command.Environment![Fetchle.Core.Storage.FetchleDirs.DATA_DIR_VARIABLE]).IsEqualTo("data-live");
        }
    }

    [Test]
    public async Task Rg_walks_hidden_and_ignored_files_with_a_case_insensitive_glob()
    {
        var args = Plan(Everything, "rg --files --iglob").Timed.Arguments;
        await Assert.That(args).IsEquivalentTo(["--files", "--hidden", "--no-ignore", "--iglob", "*settings*", ROOT]);
        await Assert.That(Plan(Everything, "rg -j1 --files --iglob").Timed.Arguments[0]).IsEqualTo("-j1");
    }

    [Test]
    public async Task Fd_is_hidden_unignored_and_case_insensitive()
    {
        var args = Plan(Everything, "fd -H -I -i").Timed.Arguments;
        await Assert.That(args).Contains("-H").And.Contains("-I").And.Contains("-i");
        await Assert.That(args[^2..]).IsEquivalentTo(["settings", ROOT]);
    }

    [Test]
    public async Task Powershell_recurses_with_force_and_a_name_filter()
    {
        var script = Plan(Everything, "Get-ChildItem -Recurse").Timed.Arguments[^1];
        await Assert.That(script).Contains("-Recurse -Force -Filter *settings*").And.Contains(ROOT);
    }

    [Test]
    public async Task Powershell_script_survives_a_quote_in_the_root()
    {
        var plans = ToolPlans.For(Everything, "it's", Dirs);
        var script = plans.Single(p => p.Timed.Tool == "Get-ChildItem -Recurse").Timed.Arguments[^1];
        await Assert.That(script).Contains("it''s' -Recurse");
    }

    [Test]
    public async Task Cmd_dir_and_find_match_on_the_name_pattern()
    {
        await Assert.That(Plan(Everything, "cmd dir /s /b").Timed.Arguments[^1]).EndsWith("*settings*");
        await Assert.That(Plan(Everything, "find -iname").Timed.Arguments).IsEquivalentTo([ROOT, "-iname", "*settings*"]);
    }

    [Test]
    public async Task Display_hides_the_corpus_root_and_exe_directory()
    {
        var command = new ToolCommand("t", Path.Combine("some", "dir", "rg"), ["--files", Path.Combine("tmp", "corpus")], 0);
        await Assert.That(ToolMeasurer.Display(command, Path.Combine("tmp", "corpus"))).IsEqualTo("rg --files {corpus}");
    }
}
