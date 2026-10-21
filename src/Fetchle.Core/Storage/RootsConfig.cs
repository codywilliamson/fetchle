using System.Text.Json;
using System.Text.Json.Serialization;
using Fetchle.Core.Walking;

namespace Fetchle.Core.Storage;

public sealed record RootsFile(List<string> Roots);

[JsonSerializable(typeof(RootsFile))]
// a missing roots list or a null root is a JsonException, so a hand-edited config fails loudly instead of later
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, WriteIndented = true,
    RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true)]
partial class StorageJson : JsonSerializerContext;

// the configured roots, in <data dir>/config.json. a missing file means no roots
public static class RootsConfig
{
    public static List<string> Load(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            // a top-level null deserializes to null, which is a damaged file, not an empty one
            var roots = JsonSerializer.Deserialize(File.ReadAllText(path), StorageJson.Default.RootsFile)?.Roots
                ?? throw new InvalidDataException($"can't read {path}: empty config");
            // nullable annotations don't reach list elements, and every caller compares roots as paths
            foreach (var root in roots)
            {
                if (string.IsNullOrWhiteSpace(root))
                {
                    throw new InvalidDataException($"can't read {path}: a root is null or blank");
                }

                if (!IsValidRoot(root))
                {
                    throw new InvalidDataException($"can't read {path}: '{root}' isn't a full path");
                }
            }
            return roots;
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"can't read {path}: {e.Message}", e);
        }
    }

    public static void Save(string path, IReadOnlyList<string> roots)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(new RootsFile([.. roots]), StorageJson.Default.RootsFile) + "\n");
        File.Move(temp, path, overwrite: true);
    }

    // what roots add writes: fully qualified and parseable. a hand edit can break either
    static bool IsValidRoot(string root)
    {
        try
        {
            return Path.IsPathFullyQualified(root) && Path.GetFullPath(root).Length > 0;
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    public static string Normalize(string root) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

    // true when path is root itself or somewhere under it, compared the way root's volume compares paths
    public static bool Covers(string root, string path) => Covers(root, path, PathComparison.For(root));

    public static bool Covers(string root, string path, StringComparison comparison)
    {
        if (path.Equals(root, comparison))
        {
            return true;
        }

        var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, comparison);
    }
}
