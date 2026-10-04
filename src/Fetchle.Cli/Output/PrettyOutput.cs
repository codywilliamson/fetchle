using Fetchle.Core.Search;
using XenoAtom.Ansi;
using XenoAtom.Terminal;

namespace Fetchle.Cli.Output;

// tty only: clickable paths, query match highlighted, size and age dimmed, footer.
// goes through Terminal so NO_COLOR and capability detection apply
static class PrettyOutput
{
    public static void Write(SearchResult result, string query, DateTimeOffset? now = null)
    {
        var at = now ?? DateTimeOffset.UtcNow;
        foreach (var hit in result.Hits)
        {
            Terminal.BeginLink(new Uri(hit.Path).AbsoluteUri);
            WriteHighlighted(hit.Path, query);
            Terminal.EndLink();
            Terminal.Decorate(AnsiDecorations.Dim);
            Terminal.Write(hit.Size is { } size ? $"  {Humanize.Size(size)}  {Humanize.Age(at - hit.Modified)}" : $"  {Humanize.Age(at - hit.Modified)}");
            Terminal.ResetStyle();
            Terminal.WriteLine();
        }
        Terminal.Decorate(AnsiDecorations.Dim);
        Terminal.Write(result.Footer());
        Terminal.ResetStyle();
        Terminal.WriteLine();
    }

    static void WriteHighlighted(string path, string query)
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
