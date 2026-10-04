using System.Globalization;

namespace Fetchle.Cli;

static class Humanize
{
    static readonly string[] SizeUnits = ["B", "KB", "MB", "GB", "TB"];

    public static string Size(long bytes)
    {
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < SizeUnits.Length - 1) { value /= 1024; unit++; }
        var format = unit == 0 || value >= 10 ? "0" : "0.0";
        return $"{value.ToString(format, CultureInfo.InvariantCulture)} {SizeUnits[unit]}";
    }

    public static string Age(TimeSpan age) => age switch
    {
        { TotalMinutes: < 1 } => "just now",
        { TotalHours: < 1 } => $"{(int)age.TotalMinutes}m ago",
        { TotalDays: < 1 } => $"{(int)age.TotalHours}h ago",
        { TotalDays: < 365 } => $"{(int)age.TotalDays}d ago",
        _ => $"{(int)(age.TotalDays / 365)}y ago",
    };
}
