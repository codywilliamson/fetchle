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
            return JsonSerializer.Deserialize(File.ReadAllText(path), StorageJson.Default.RootsFile)?.Roots ?? [];
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

    public static string Normalize(string root) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

    // true when path is root itself or somewhere under it
    public static bool Covers(string root, string path)
    {
        if (path.Equals(root, PathComparison.Default))
        {
            return true;
        }

        var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, PathComparison.Default);
    }
}
