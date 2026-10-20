namespace Fetchle.Cli.Install;

// how a PATH-style string splits and compares on one platform
internal readonly record struct PathRules(char Separator, bool IgnoreCase, bool ExpandVariables)
{
    public static readonly PathRules Windows = new(';', true, true);
    public static readonly PathRules Unix = new(':', false, false);

    public static PathRules Current => OperatingSystem.IsWindows() ? Windows : Unix;

    public string Key(string entry)
    {
        var text = ExpandVariables ? Environment.ExpandEnvironmentVariables(entry) : entry;
        text = text.Trim().TrimEnd('/', '\\');
        return IgnoreCase ? text.ToUpperInvariant() : text;
    }
}

// pure edits on a PATH string. entries match by key, so a trailing slash or an env var spelling doesn't hide one
internal static class PathEntries
{
    public static int Count(string? path, string entry, PathRules rules)
    {
        if (string.IsNullOrEmpty(path))
        {
            return 0;
        }

        var key = rules.Key(entry);
        return path.Split(rules.Separator).Count(part => rules.Key(part) == key);
    }

    // puts entry first when absent. when present it keeps the first copy and drops the rest
    public static string Add(string? path, string entry, PathRules rules)
    {
        if (string.IsNullOrEmpty(path))
        {
            return entry;
        }

        var key = rules.Key(entry);
        var parts = path.Split(rules.Separator);
        if (!parts.Any(part => rules.Key(part) == key))
        {
            return entry + rules.Separator + path;
        }

        var result = new List<string>(parts.Length);
        var seen = false;
        foreach (var part in parts)
        {
            if (rules.Key(part) == key)
            {
                if (seen)
                {
                    continue;
                }

                seen = true;
            }

            result.Add(part);
        }
        return string.Join(rules.Separator, result);
    }

    public static string Remove(string? path, string entry, PathRules rules)
    {
        if (string.IsNullOrEmpty(path))
        {
            return "";
        }

        var key = rules.Key(entry);
        var kept = path.Split(rules.Separator).Where(part => rules.Key(part) != key).ToList();
        return kept.All(string.IsNullOrEmpty) ? "" : string.Join(rules.Separator, kept);
    }
}
