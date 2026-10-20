using Microsoft.Extensions.Logging.Abstractions;
using Fetchle.Bench;

namespace Fetchle.Bench.Tests;

// drives the real `dotnet` host as the stand-in tool, since it is the one process every machine here has
public class ToolMeasurerTests
{
    static ToolMeasurer Measurer() => new(new ProcessTimer(), NullLogger<ToolMeasurer>.Instance);

    static ToolPlan Dotnet(string tool, string[] args, int expectedExit) => new(new ToolCommand(tool, "dotnet", args, expectedExit), []);

    [Test]
    public async Task Good_tool_gets_twenty_runs_and_ordered_percentiles()
    {
        var result = Measurer().Measure(Dotnet("dotnet", ["--version"], 0), "realistic", "root");

        await Assert.That(result.Status).IsEqualTo(ResultStatus.OK);
        await Assert.That(result.RunsMs.Length).IsEqualTo(ToolMeasurer.TIMED_RUNS);
        await Assert.That(result.P50Ms!.Value).IsLessThanOrEqualTo(result.P95Ms!.Value);
        await Assert.That(result.RunsMs).Contains(result.P95Ms!.Value);
    }

    [Test]
    public async Task A_tool_over_the_time_guard_is_skipped_after_its_warmup_and_the_json_says_why()
    {
        var measurer = new ToolMeasurer(new ProcessTimer(), NullLogger<ToolMeasurer>.Instance, TimeSpan.FromMilliseconds(1));

        var result = measurer.Measure(Dotnet("dotnet", ["--version"], 0), "home-1m", "root");

        await Assert.That(result.Status).IsEqualTo(ResultStatus.SKIPPED);
        await Assert.That(result.Reason).Contains("guard");
        await Assert.That(result.RunsMs.Length).IsEqualTo(1);
        await Assert.That(result.P50Ms).IsNull();
    }

    [Test]
    public async Task Wrong_exit_code_is_recorded_as_a_failure_with_the_command_output()
    {
        var result = Measurer().Measure(Dotnet("dotnet bad", ["--no-such-switch"], 0), "realistic", "root");

        await Assert.That(result.Status).IsEqualTo(ResultStatus.FAILED);
        await Assert.That(result.Reason).Contains("expected 0");
        await Assert.That(result.P50Ms).IsNull();
    }

    [Test]
    public async Task Failing_setup_fails_the_plan_before_any_timing()
    {
        var setup = new ToolCommand("setup", "dotnet", ["--no-such-switch"], 0);
        var plan = new ToolPlan(new ToolCommand("dotnet", "dotnet", ["--version"], 0), [setup]);

        var result = Measurer().Measure(plan, "realistic", "root");

        await Assert.That(result.Status).IsEqualTo(ResultStatus.FAILED);
        await Assert.That(result.RunsMs).IsEmpty();
    }

    [Test]
    public async Task Before_each_run_hook_fires_for_the_warmup_and_every_timed_run()
    {
        var calls = 0;
        var command = new ToolCommand("dotnet", "dotnet", ["--version"], 0, BeforeEachRun: () => calls++);

        Measurer().Measure(new ToolPlan(command, []), "realistic", "root");

        await Assert.That(calls).IsEqualTo(ToolMeasurer.TIMED_RUNS + 1);
    }
}
