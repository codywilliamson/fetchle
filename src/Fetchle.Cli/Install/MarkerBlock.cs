namespace Fetchle.Cli.Install;

internal enum BlockChange
{
    Unchanged,
    Inserted,
    Replaced,
}

internal readonly record struct BlockEdit(string Content, BlockChange Change);

// the `# >>> fetchle >>>` block fetchle owns inside a file someone else owns. all edits are pure string work
internal static class MarkerBlock
{
    public const string START = "# >>> fetchle >>>";
    public const string END = "# <<< fetchle <<<";

    public static string Render(string body) => $"{START}\n{body}\n{END}";

    // each complete block's text, markers included. a start with no end is left alone
    public static List<string> Find(string content)
    {
        var lines = content.Split('\n');
        return Locate(lines).Select(span => string.Join('\n', lines[span.Start..(span.End + 1)].Select(TrimCr))).ToList();
    }

    // puts the block in: appended when absent, replaced in place when present, extra copies dropped
    public static BlockEdit Apply(string content, string body)
    {
        var block = Render(body);
        var lines = content.Split('\n').ToList();
        var spans = Locate(lines);
        if (spans.Count == 0)
        {
            return new BlockEdit(Append(content, block), BlockChange.Inserted);
        }

        var current = string.Join('\n', lines.GetRange(spans[0].Start, spans[0].End - spans[0].Start + 1).Select(TrimCr));
        if (spans.Count == 1 && current == block)
        {
            return new BlockEdit(content, BlockChange.Unchanged);
        }

        for (var i = spans.Count - 1; i >= 1; i--)
        {
            lines.RemoveRange(spans[i].Start, spans[i].End - spans[i].Start + 1);
        }

        lines.RemoveRange(spans[0].Start, spans[0].End - spans[0].Start + 1);
        lines.InsertRange(spans[0].Start, block.Split('\n'));
        return new BlockEdit(string.Join('\n', lines), BlockChange.Replaced);
    }

    // takes every block out, plus the one blank line Apply put in front of it
    public static string Remove(string content)
    {
        var lines = content.Split('\n').ToList();
        var spans = Locate(lines);
        for (var i = spans.Count - 1; i >= 0; i--)
        {
            var start = spans[i].Start;
            var count = spans[i].End - start + 1;
            if (start > 0 && string.IsNullOrWhiteSpace(lines[start - 1]))
            {
                start--;
                count++;
            }

            lines.RemoveRange(start, count);
        }
        return string.Join('\n', lines);
    }

    static string Append(string content, string block)
    {
        if (content.Length == 0)
        {
            return block + "\n";
        }

        var separator = content.EndsWith('\n') ? "\n" : "\n\n";
        return content + separator + block + "\n";
    }

    static List<(int Start, int End)> Locate(IReadOnlyList<string> lines)
    {
        var spans = new List<(int, int)>();
        var start = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = TrimCr(lines[i]);
            if (line == START)
            {
                start = i;
            }
            else if (line == END && start >= 0)
            {
                spans.Add((start, i));
                start = -1;
            }
        }
        return spans;
    }

    static string TrimCr(string line) => line.TrimEnd('\r');
}
