using System.Runtime.Versioning;

namespace Fetchle.Core.Walking.Windows;

// an open directory handle shared by the listing of that dir and every child task not yet opened.
// starts at 1 for the listing, +1 per pushed child. the last release closes it
[SupportedOSPlatform("windows")]
sealed class DirHandle(nint handle)
{
    int _refs = 1;

    public nint Handle { get; } = handle;

    public void AddRef() => Interlocked.Increment(ref _refs);

    public void Release()
    {
        if (Interlocked.Decrement(ref _refs) == 0)
        {
            NtApi.Close(Handle);
        }
    }
}
