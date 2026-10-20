namespace Fetchle.Bench;

// Setup runs once, untimed, before the timed command. a setup step that fails fails the whole plan
public sealed record ToolPlan(ToolCommand Timed, IReadOnlyList<ToolCommand> Setup);
