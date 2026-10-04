using System.Globalization;
using XenoAtom.CommandLine;

namespace Fetchle.Cli.Commands;

// parses "250ms", "2s", "1.5m", "1h", "3d"
static class Durations
{
    public static bool TryParse(string? text, out TimeSpan duration)
    {
        duration = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var s = text.AsSpan().Trim();
        var unitStart = 0;
        while (unitStart < s.Length && (char.IsAsciiDigit(s[unitStart]) || s[unitStart] == '.')) unitStart++;
        if (unitStart == 0 || !double.TryParse(s[..unitStart], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
            return false;
        double? ms = s[unitStart..] switch
        {
            "ms" => value,
            "s" => value * 1_000,
            "m" => value * 60_000,
            "h" => value * 3_600_000,
            "d" => value * 86_400_000,
            _ => null,
        };
        if (ms is not { } total) return false;
        duration = TimeSpan.FromMilliseconds(total);
        return true;
    }

    public static TimeSpan Parse(string? text, string option) =>
        TryParse(text, out var d) ? d : throw new CommandOptionException($"invalid duration '{text}', use e.g. 250ms, 2s, 3d", option);
}
