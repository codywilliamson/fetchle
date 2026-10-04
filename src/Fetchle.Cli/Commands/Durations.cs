using System.Collections.Frozen;
using System.Globalization;
using System.Text.RegularExpressions;
using XenoAtom.CommandLine;

namespace Fetchle.Cli.Commands;

static partial class Durations
{
    static readonly FrozenDictionary<string, TimeSpan> Units = new Dictionary<string, TimeSpan>
    {
        ["ms"] = TimeSpan.FromMilliseconds(1),
        ["s"] = TimeSpan.FromSeconds(1),
        ["m"] = TimeSpan.FromMinutes(1),
        ["h"] = TimeSpan.FromHours(1),
        ["d"] = TimeSpan.FromDays(1),
    }.ToFrozenDictionary();

    [GeneratedRegex(@"^\s*(?<value>\d+(\.\d+)?)(?<unit>ms|s|m|h|d)\s*$")]
    private static partial Regex Pattern();

    public static bool TryParse(string? text, out TimeSpan duration)
    {
        duration = default;
        var match = Pattern().Match(text ?? "");
        if (!match.Success)
        {
            return false;
        }

        var value = double.Parse(match.Groups["value"].ValueSpan, CultureInfo.InvariantCulture);
        var milliseconds = value * Units[match.Groups["unit"].Value].TotalMilliseconds;
        if (milliseconds > TimeSpan.MaxValue.TotalMilliseconds)
        {
            return false;
        }

        duration = TimeSpan.FromMilliseconds(milliseconds);
        return true;
    }

    public static TimeSpan Parse(string? text, string option) =>
        TryParse(text, out var duration)
            ? duration
            : throw new CommandOptionException($"invalid duration '{text}', use e.g. 250ms, 2s, 3d", option);
}
