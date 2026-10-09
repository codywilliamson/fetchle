using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Fetchle.Core.Walking.Windows;

// lists dirs with NtCreateFile relative to the parent's handle and NtQueryDirectoryFileEx into a 64 KB buffer.
// children are only ever opened relative to their parent, never by full path, so a parent swapped for a junction can't redirect the walk.
// failed opens (access denied, vanished, delete pending, sharing violation, not a dir, swapped for a link, out of resources...) skip the dir silently
[SupportedOSPlatform("windows")]
sealed unsafe class WindowsDirectoryLister : IDirectoryLister
{
    const char PATH_SEPARATOR = '\\';
    const int BUFFER_SIZE = 64 * 1024;
    const uint ATTRIBUTE_DIRECTORY = 0x10;
    const uint ATTRIBUTE_REPARSE_POINT = 0x400;

    // the root follows links like the .NET lister does. children open the entry itself, never its target
    public const uint ROOT_OPTIONS = NtApi.FILE_DIRECTORY_FILE | NtApi.FILE_SYNCHRONOUS_IO_NONALERT;
    public const uint CHILD_OPTIONS = ROOT_OPTIONS | NtApi.FILE_OPEN_REPARSE_POINT;

    byte* _buffer;

    public bool List(in DirTask dir, WalkWorker owner)
    {
        var parent = dir.Parent as DirHandle;
        DirHandle? self;
        try
        {
            self = Open(dir.Path, parent);
        }
        finally
        {
            parent?.Release();
        }

        if (self is null)
        {
            return true;
        }

        try
        {
            return ListOpen(self, dir.Path, owner);
        }
        finally
        {
            self.Release();
        }
    }

    public void Discard(in DirTask dir) => (dir.Parent as DirHandle)?.Release();

    public void Dispose()
    {
        if (_buffer is not null)
        {
            NativeMemory.AlignedFree(_buffer);
            _buffer = null;
        }
    }

    // \??\C:\x, \??\UNC\server\share\x. \\?\ and \\.\ paths keep their exact spelling (trailing dots, CON...)
    public static string ToNtPath(string fullPath)
    {
        if (fullPath.StartsWith(@"\\?\", StringComparison.Ordinal) || fullPath.StartsWith(@"\\.\", StringComparison.Ordinal))
        {
            return string.Concat(@"\??\", fullPath.AsSpan(4));
        }

        return fullPath.StartsWith(@"\\", StringComparison.Ordinal)
            ? string.Concat(@"\??\UNC\", fullPath.AsSpan(2))
            : string.Concat(@"\??\", fullPath);
    }

    static DirHandle? Open(string path, DirHandle? parent)
    {
        int status;
        nint handle;
        if (parent is null)
        {
            // a typed root may differ in case from what's on disk; CreateFileW always passes this too
            status = NtApi.OpenDirectory(0, ToNtPath(path), NtApi.OBJ_CASE_INSENSITIVE, ROOT_OPTIONS, out handle);
        }
        else
        {
            var name = path.AsSpan(path.LastIndexOf(PATH_SEPARATOR) + 1);
            status = NtApi.OpenDirectory(parent.Handle, name, NtApi.OBJ_DONT_REPARSE, CHILD_OPTIONS, out handle);
        }

        return status >= 0 ? new DirHandle(handle) : null;
    }

    bool ListOpen(DirHandle dir, string path, WalkWorker owner)
    {
        if (_buffer is null)
        {
            _buffer = (byte*)NativeMemory.AlignedAlloc(BUFFER_SIZE, 8);
        }

        // no SL_RETURN_ON_DISK_ENTRIES_ONLY: on virtualized roots it would hide projected entries the .NET lister shows
        var flags = NtApi.SL_RESTART_SCAN;

        while (true)
        {
            var status = NtApi.QueryDirectory(dir.Handle, _buffer, BUFFER_SIZE, flags, out var bytes);

            // STATUS_NO_MORE_FILES is the normal end. anything else ends this dir too
            if (status != 0 || bytes == 0)
            {
                return true;
            }

            flags = 0;
            if (!VisitBuffer(dir, path, owner))
            {
                return false;
            }
        }
    }

    // false when the walk was stopped partway
    bool VisitBuffer(DirHandle dir, string path, WalkWorker owner)
    {
        var entry = _buffer;
        while (true)
        {
            var next = *(uint*)entry;
            var attributes = *(uint*)(entry + NtApi.ENTRY_ATTRIBUTES);
            var name = new ReadOnlySpan<char>(entry + NtApi.ENTRY_NAME, (int)(*(uint*)(entry + NtApi.ENTRY_NAME_LENGTH) / 2));
            while (name.Length > 0 && name[^1] == '\0')
            {
                name = name[..^1];
            }

            if ((attributes & ATTRIBUTE_REPARSE_POINT) == 0 && !IsDotEntry(name))
            {
                if (owner.ShouldStop())
                {
                    return false;
                }

                VisitEntry(dir, path, name, (attributes & ATTRIBUTE_DIRECTORY) != 0, owner);
            }

            if (next == 0)
            {
                return true;
            }

            entry += next;
        }
    }

    static void VisitEntry(DirHandle dir, string path, ReadOnlySpan<char> name, bool isDirectory, WalkWorker owner)
    {
        var walkEntry = new WalkEntry(path, name, isDirectory);
        if (isDirectory)
        {
            // a name with a separator or NUL can't be one component, so it can't be opened relative to us
            if (owner.Prune.ShouldPrune(path, name) || name.IndexOfAny(PATH_SEPARATOR, '\0') >= 0)
            {
                return;
            }

            dir.AddRef();
            owner.Push(new DirTask(walkEntry.ToFullPath(), dir));
        }

        owner.Visit(ref walkEntry);
    }

    static bool IsDotEntry(ReadOnlySpan<char> name) => name is "." or "..";
}
