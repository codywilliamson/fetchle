using XenoAtom.CommandLine;

namespace Fetchle.Cli;

static class SearchCommand
{
    public static ValueTask<int> RunAsync(CommandRunContext ctx, SearchArgs args)
    {
        ctx.Error.WriteLine("fetchle: search not implemented yet");
        return ValueTask.FromResult(ExitCodes.Usage);
    }
}
