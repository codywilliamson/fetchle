using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Fetchle.Core.Walking.Windows;

// the few ntdll calls the native lister needs. values checked against ntstatus.h and the NtQueryDirectoryFileEx docs
[SupportedOSPlatform("windows")]
static unsafe partial class NtApi
{
    const uint FILE_LIST_DIRECTORY = 0x1;
    const uint SYNCHRONIZE = 0x100000;
    const uint SHARE_ALL = 0x7;
    const uint FILE_OPEN = 0x1;

    public const uint OBJ_CASE_INSENSITIVE = 0x40;
    public const uint OBJ_DONT_REPARSE = 0x1000;
    public const uint FILE_DIRECTORY_FILE = 0x1;
    public const uint FILE_SYNCHRONOUS_IO_NONALERT = 0x20;
    public const uint FILE_OPEN_REPARSE_POINT = 0x200000;

    public const uint SL_RESTART_SCAN = 0x1;
    const int FILE_DIRECTORY_INFORMATION = 1;

    public const int STATUS_INVALID_PARAMETER = unchecked((int)0xC000000D);

    // FILE_DIRECTORY_INFORMATION offsets
    public const int ENTRY_ATTRIBUTES = 56;
    public const int ENTRY_NAME_LENGTH = 60;
    public const int ENTRY_NAME = 64;

    static int _liveHandles;

    // handles opened and not yet closed, across all threads. tests assert it's back to 0 after a walk
    public static int LiveHandles => Volatile.Read(ref _liveHandles);

    [StructLayout(LayoutKind.Sequential)]
    struct UNICODE_STRING
    {
        public ushort Length;
        public ushort MaximumLength;
        public char* Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct OBJECT_ATTRIBUTES
    {
        public int Length;
        public nint RootDirectory;
        public UNICODE_STRING* ObjectName;
        public uint Attributes;
        public nint SecurityDescriptor;
        public nint SecurityQualityOfService;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct IO_STATUS_BLOCK
    {
        public nint Status;
        public nint Information;
    }

    [LibraryImport("ntdll.dll")]
    private static partial int NtCreateFile(out nint handle, uint access, OBJECT_ATTRIBUTES* attributes, IO_STATUS_BLOCK* ioStatus,
        long* allocationSize, uint fileAttributes, uint share, uint disposition, uint options, nint eaBuffer, uint eaLength);

    [LibraryImport("ntdll.dll")]
    private static partial int NtClose(nint handle);

    [LibraryImport("ntdll.dll")]
    private static partial int NtQueryDirectoryFileEx(nint handle, nint evt, nint apcRoutine, nint apcContext, IO_STATUS_BLOCK* ioStatus,
        void* buffer, uint length, int informationClass, uint queryFlags, UNICODE_STRING* fileName);

    // name is one component when parent is a handle, a full \??\ path when it's 0
    public static int OpenDirectory(nint parent, ReadOnlySpan<char> name, uint attributes, uint options, out nint handle)
    {
        handle = 0;
        if (name.Length > ushort.MaxValue / 2)
        {
            return STATUS_INVALID_PARAMETER;
        }

        fixed (char* chars = name)
        {
            var path = new UNICODE_STRING { Length = (ushort)(name.Length * 2), MaximumLength = (ushort)(name.Length * 2), Buffer = chars };
            var oa = new OBJECT_ATTRIBUTES { Length = sizeof(OBJECT_ATTRIBUTES), RootDirectory = parent, ObjectName = &path, Attributes = attributes };
            IO_STATUS_BLOCK io;
            var status = NtCreateFile(out handle, FILE_LIST_DIRECTORY | SYNCHRONIZE, &oa, &io, null, 0, SHARE_ALL, FILE_OPEN,
                options, 0, 0);
            if (status >= 0)
            {
                Interlocked.Increment(ref _liveHandles);
            }
            else
            {
                handle = 0;
            }

            return status;
        }
    }

    public static void Close(nint handle)
    {
        NtClose(handle);
        Interlocked.Decrement(ref _liveHandles);
    }

    // returns the status, and how many bytes of entries landed in the buffer
    public static int QueryDirectory(nint handle, byte* buffer, uint length, uint flags, out int bytes)
    {
        IO_STATUS_BLOCK io;
        var status = NtQueryDirectoryFileEx(handle, 0, 0, 0, &io, buffer, length, FILE_DIRECTORY_INFORMATION, flags, null);
        bytes = (int)io.Information;
        return status;
    }
}
