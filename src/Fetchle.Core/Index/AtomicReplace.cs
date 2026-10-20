using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Fetchle.Core.Index;

// renames source over target in one step, even while readers have target memory-mapped.
// unix rename(2) already does that. on windows a plain File.Move gets access denied against a mapped target,
// a posix-semantics rename doesn't (spike in docs/decisions.md, 2026-10-20)
static partial class AtomicReplace
{
    public static void Move(string source, string target)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.Move(source, target, overwrite: true);
            return;
        }

        var error = WindowsPosixRename(source, target);
        if (error == 0)
        {
            return;
        }

        // FAT and older filesystems don't do posix semantics; a plain replace still works when nobody has it mapped
        if (error is ERROR_INVALID_PARAMETER or ERROR_NOT_SUPPORTED or ERROR_INVALID_FUNCTION)
        {
            File.Move(source, target, overwrite: true);
            return;
        }

        throw new IOException($"can't replace {target}: win32 error {error}", Marshal.GetHRForLastWin32Error());
    }

    const int ERROR_INVALID_FUNCTION = 1;
    const int ERROR_INVALID_PARAMETER = 87;
    const int ERROR_NOT_SUPPORTED = 50;

    const uint DELETE = 0x0001_0000;
    const uint SYNCHRONIZE = 0x0010_0000;
    const uint SHARE_ALL = 7;
    const uint OPEN_EXISTING = 3;
    const int FILE_RENAME_INFO_EX = 22;
    const uint FILE_RENAME_FLAG_REPLACE_IF_EXISTS = 1;
    const uint FILE_RENAME_FLAG_POSIX_SEMANTICS = 2;

    // FILE_RENAME_INFO: ULONG Flags; HANDLE RootDirectory; DWORD FileNameLength; WCHAR FileName[]
    const int FLAGS_OFFSET = 0;
    const int ROOT_OFFSET = 8;
    const int NAME_LENGTH_OFFSET = 16;
    const int NAME_OFFSET = 20;

    static unsafe int WindowsPosixRename(string source, string target)
    {
        using var handle = CreateFileW(Path.GetFullPath(source), DELETE | SYNCHRONIZE, SHARE_ALL, 0, OPEN_EXISTING, 0, 0);
        if (handle.IsInvalid)
        {
            return Marshal.GetLastPInvokeError();
        }

        var name = Path.GetFullPath(target);
        var size = NAME_OFFSET + (name.Length + 1) * sizeof(char);
        var buffer = new byte[size];
        fixed (byte* info = buffer)
        {
            *(uint*)(info + FLAGS_OFFSET) = FILE_RENAME_FLAG_REPLACE_IF_EXISTS | FILE_RENAME_FLAG_POSIX_SEMANTICS;
            *(nint*)(info + ROOT_OFFSET) = 0;
            *(uint*)(info + NAME_LENGTH_OFFSET) = (uint)(name.Length * sizeof(char));
            name.AsSpan().CopyTo(new Span<char>(info + NAME_OFFSET, name.Length));
            return SetFileInformationByHandle(handle, FILE_RENAME_INFO_EX, info, (uint)size) ? 0 : Marshal.GetLastPInvokeError();
        }
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateFileW(string name, uint access, uint share, nint security, uint disposition, uint flags, nint template);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool SetFileInformationByHandle(SafeFileHandle handle, int infoClass, byte* info, uint size);
}
