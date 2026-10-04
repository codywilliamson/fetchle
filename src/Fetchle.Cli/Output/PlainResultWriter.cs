using Fetchle.Core.Search;

namespace Fetchle.Cli.Output;

sealed class PlainResultWriter(TextWriter output, bool footer) : IResultWriter
{
    public void Write(SearchResult result)
    {
        foreach (var hit in result.Hits)
        {
            output.WriteLine(hit.Path);
        }

        if (footer)
        {
            output.WriteLine(result.Footer());
        }
    }
}
