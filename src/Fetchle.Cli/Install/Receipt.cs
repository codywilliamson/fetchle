using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fetchle.Cli.Install;

// what setup did to the machine, so uninstall can undo exactly that
internal sealed record Receipt(string Version, string Channel, string InstallDir, string DataDir, PathEdit? PathEdit, List<AgentRegistration> Agents);

internal sealed record PathEdit(string Kind, string Entry, string? File = null);

internal sealed record AgentRegistration(string Agent, string Kind, string File);

internal static class PathEditKinds
{
    public const string WINDOWS_USER_PATH = "windows-user-path";
    public const string SHELL_RC_BLOCK = "shell-rc-block";
}

internal static class Channels
{
    public const string SCRIPT = "script";
}

[JsonSerializable(typeof(Receipt))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class ReceiptJson : JsonSerializerContext;

internal static class ReceiptStore
{
    // null when there's no receipt. throws InvalidDataException when there is one that can't be read
    public static Receipt? Load(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(path), ReceiptJson.Default.Receipt)
                ?? throw new InvalidDataException($"can't read {path}: empty receipt");
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"can't read {path}: {e.Message}", e);
        }
    }

    public static void Save(string path, Receipt receipt)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(receipt, ReceiptJson.Default.Receipt) + "\n");
        File.Move(temp, path, overwrite: true);
    }
}
