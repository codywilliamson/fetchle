namespace Fetchle.Cli.Commands;

// docs/specs/cli.md#exit-codes
static class ExitCodes
{
    public const int Success = 0;
    public const int NoResults = 1;
    public const int Usage = 2;
    public const int IndexMissing = 3;
    public const int BudgetExpired = 4;
}
