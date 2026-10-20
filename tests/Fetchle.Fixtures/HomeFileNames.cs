namespace Fetchle.Fixtures;

// file names for one directory, unique within it (case-insensitively, since NTFS is)
static class HomeFileNames
{
    const double DOTFILE_CHANCE = 0.015;
    const int GIT_OBJECT_HEX_LENGTH = 38;
    const int CACHE_HEX_LENGTH = 32;

    public static List<string> For(FileKind kind, int count, Random rng, IEnumerable<string> alreadyTaken)
    {
        var taken = new HashSet<string>(alreadyTaken, StringComparer.OrdinalIgnoreCase);
        var names = new List<string>(count);
        for (var i = 0; names.Count < count; i++)
        {
            var (stem, ext) = Next(kind, rng);
            var name = stem + ext;
            if (!taken.Add(name))
            {
                // the loop index makes the retry unique even in a bulk dir
                name = $"{stem}-{i}{ext}";
                if (!taken.Add(name))
                {
                    continue;
                }
            }
            names.Add(name);
        }
        return names;
    }

    static (string Stem, string Ext) Next(FileKind kind, Random rng)
    {
        if (kind is FileKind.Code or FileKind.Npm or FileKind.Data && rng.NextDouble() < DOTFILE_CHANCE)
        {
            return (HomeWords.Dotfiles[rng.Next(HomeWords.Dotfiles.Length)], "");
        }

        return kind switch
        {
            FileKind.Code => (CodeStem(rng), Pick(rng, HomeWords.CodeExts)),
            FileKind.Npm => (HomeWords.NpmStems[rng.Next(HomeWords.NpmStems.Length)] + (rng.Next(3) == 0 ? rng.Next(1, 30).ToString() : ""), Pick(rng, HomeWords.NpmExts)),
            FileKind.Media => (MediaStem(rng), Pick(rng, HomeWords.MediaExts)),
            FileKind.Document => ($"{Word(rng)} {Word(rng)} {rng.Next(1, 100)}", Pick(rng, HomeWords.DocumentExts)),
            FileKind.GitObject => (Hex(rng, GIT_OBJECT_HEX_LENGTH), ""),
            FileKind.Hex => (Hex(rng, CACHE_HEX_LENGTH), rng.Next(4) == 0 ? ".tmp" : ""),
            _ => (DataStem(rng), Pick(rng, HomeWords.DataExts)),
        };
    }

    static string CodeStem(Random rng) => rng.Next(10) switch
    {
        < 5 => Pascal(Word(rng)),
        < 8 => Pascal(Word(rng)) + Pascal(Word(rng)),
        _ => $"{Word(rng)}_{Word(rng)}",
    };

    static string MediaStem(Random rng) => rng.Next(3) switch
    {
        0 => $"IMG_{rng.Next(10000):D4}",
        1 => $"{Word(rng)}-{rng.Next(1, 500)}",
        _ => $"{Word(rng)}_{rng.Next(100000):D5}",
    };

    static string DataStem(Random rng) => rng.Next(3) switch
    {
        0 => $"data_{rng.Next(100)}",
        1 => Word(rng),
        _ => $"{Word(rng)}{rng.Next(1000):D3}",
    };

    static string Word(Random rng) => HomeWords.Stems[rng.Next(HomeWords.Stems.Length)];

    static string Pascal(string word) => char.ToUpperInvariant(word[0]) + word[1..];

    static string Hex(Random rng, int length)
    {
        Span<char> buffer = stackalloc char[length];
        for (var i = 0; i < length; i++)
        {
            buffer[i] = "0123456789abcdef"[rng.Next(16)];
        }
        return new string(buffer);
    }

    static string Pick(Random rng, (string Ext, int Weight)[] table)
    {
        var total = 0;
        foreach (var (_, weight) in table)
        {
            total += weight;
        }
        var roll = rng.Next(total);
        foreach (var (ext, weight) in table)
        {
            roll -= weight;
            if (roll < 0)
            {
                return ext;
            }
        }
        return table[^1].Ext;
    }
}
