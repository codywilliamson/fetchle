using Fetchle.Core;

namespace Fetchle.Cli;

// plain: one path per line. agent: the same plus one footer line
static class PlainOutput
{
    public static void Write(TextWriter output, SearchResult result, bool footer)
    {
        foreach (var hit in result.Hits) output.WriteLine(hit.Path);
        if (footer) output.WriteLine(result.Footer());
    }
}
