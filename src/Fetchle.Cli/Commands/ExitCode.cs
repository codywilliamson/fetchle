namespace Fetchle.Cli.Commands;

public enum ExitCode
{
    Success = 0,
    NoResults = 1,
    Usage = 2,
    IndexMissing = 3,
    BudgetExpired = 4,
}
