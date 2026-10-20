using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Fetchle.Cli.Install;

// HKCU\Environment Path, read without expanding so %VARS% in other entries survive a rewrite
[SupportedOSPlatform("windows")]
internal sealed partial class WindowsUserPath : IPathPersistence
{
    const string ENVIRONMENT_KEY = "Environment";
    const string PATH_VALUE = "Path";
    const nint HWND_BROADCAST = 0xffff;
    const uint WM_SETTINGCHANGE = 0x001A;
    const uint SMTO_ABORTIFHUNG = 0x0002;
    const uint BROADCAST_TIMEOUT_MS = 2000;

    public int Count(string entry) => PathEntries.Count(Read(), entry, PathRules.Windows);

    public PathEdit Add(string entry)
    {
        var current = Read();
        var updated = PathEntries.Add(current, entry, PathRules.Windows);
        if (updated != current)
        {
            Write(updated);
        }

        return new PathEdit(PathEditKinds.WINDOWS_USER_PATH, entry);
    }

    public void Remove(string entry)
    {
        var current = Read();
        var updated = PathEntries.Remove(current, entry, PathRules.Windows);
        if (updated == current)
        {
            return;
        }

        if (updated.Length == 0)
        {
            using var key = Registry.CurrentUser.CreateSubKey(ENVIRONMENT_KEY);
            key.DeleteValue(PATH_VALUE, throwOnMissingValue: false);
            Broadcast();
            return;
        }

        Write(updated);
    }

    static string? Read()
    {
        using var key = Registry.CurrentUser.OpenSubKey(ENVIRONMENT_KEY);
        return key?.GetValue(PATH_VALUE, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
    }

    static void Write(string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(ENVIRONMENT_KEY);
        // an existing REG_SZ stays REG_SZ, a new value is REG_EXPAND_SZ like Windows makes it
        var kind = key.GetValue(PATH_VALUE) is null ? RegistryValueKind.ExpandString : key.GetValueKind(PATH_VALUE);
        key.SetValue(PATH_VALUE, value, kind);
        Broadcast();
    }

    // tells explorer and new terminals to reread the environment
    static void Broadcast() =>
        SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, 0, ENVIRONMENT_KEY, SMTO_ABORTIFHUNG, BROADCAST_TIMEOUT_MS, out _);

    [LibraryImport("user32.dll", EntryPoint = "SendMessageTimeoutW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint SendMessageTimeout(nint hWnd, uint msg, nuint wParam, string lParam, uint flags, uint timeout, out nuint result);
}
