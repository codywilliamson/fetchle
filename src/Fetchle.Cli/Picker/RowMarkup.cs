using System.Text;
using Fetchle.Cli.Output;
using Fetchle.Core.Search;
using XenoAtom.Ansi;

namespace Fetchle.Cli.Picker;

// one result row as terminal.ui markup: the path with the typed words highlighted, then size and age dimmed
static class RowMarkup
{
    const string SELECTED_MARK = "[bold]> [/]";
    const string UNSELECTED_MARK = "  ";

    public static string Build(SearchHit hit, string[] words, bool selected, DateTimeOffset now)
    {
        var text = new StringBuilder(hit.Path.Length + 48);
        text.Append(selected ? SELECTED_MARK : UNSELECTED_MARK);

        var at = 0;
        foreach (var (start, end) in Spans(hit.Path, words))
        {
            text.Append(AnsiMarkup.Escape(hit.Path.AsSpan(at, start - at)));
            text.Append("[bold yellow]").Append(AnsiMarkup.Escape(hit.Path.AsSpan(start, end - start))).Append("[/]");
            at = end;
        }
        text.Append(AnsiMarkup.Escape(hit.Path.AsSpan(at)));

        var age = Humanize.Age(now - hit.Modified);
        var details = hit.Size is { } size ? $"  {Humanize.Size(size)}  {age}" : $"  {age}";
        text.Append("[dim]").Append(AnsiMarkup.Escape(details)).Append("[/]");
        return text.ToString();
    }

    // the last place each word occurs, overlaps merged, in path order
    public static List<(int Start, int End)> Spans(string path, string[] words)
    {
        var found = new List<(int Start, int End)>(words.Length);
        foreach (var word in words)
        {
            var start = path.LastIndexOf(word, StringComparison.OrdinalIgnoreCase);
            if (start >= 0)
            {
                found.Add((start, start + word.Length));
            }
        }

        found.Sort();
        var merged = new List<(int Start, int End)>(found.Count);
        foreach (var span in found)
        {
            if (merged.Count > 0 && span.Start <= merged[^1].End)
            {
                merged[^1] = (merged[^1].Start, Math.Max(merged[^1].End, span.End));
            }
            else
            {
                merged.Add(span);
            }
        }
        return merged;
    }
}
