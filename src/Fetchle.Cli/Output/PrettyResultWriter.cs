using Fetchle.Core.Search;
using XenoAtom.Ansi;
using XenoAtom.Terminal;

namespace Fetchle.Cli.Output;

// writes through Terminal so NO_COLOR and capability detection apply
sealed class PrettyResultWriter(string query, TimeProvider clock) : IResultWriter
{
    public void Write(SearchResult result)
    {
        var now = clock.GetUtcNow();
        foreach (var hit in result.Hits)
        {
            Terminal.BeginLink(new Uri(hit.Path).AbsoluteUri);
            WriteHighlighted(hit.Path);
            Terminal.EndLink();
            WriteDim(Details(hit, now));
            Terminal.WriteLine();
        }

        WriteDim(result.Footer());
        Terminal.WriteLine();
    }

    static string Details(SearchHit hit, DateTimeOffset now)
    {
        var age = Humanize.Age(now - hit.Modified);
        return hit.Size is { } size ? $"  {Humanize.Size(size)}  {age}" : $"  {age}";
    }

    static void WriteDim(string text)
    {
        Terminal.Decorate(AnsiDecorations.Dim);
        Terminal.Write(text);
        Terminal.ResetStyle();
    }

    void WriteHighlighted(string path)
    {
        var at = path.LastIndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (at < 0)
        {
            Terminal.Write(path);
            return;
        }

        Terminal.Write(path[..at]);
        Terminal.Foreground(AnsiColors.Yellow);
        Terminal.Decorate(AnsiDecorations.Bold);
        Terminal.Write(path.Substring(at, query.Length));
        Terminal.ResetStyle();
        Terminal.Write(path[(at + query.Length)..]);
    }
}
