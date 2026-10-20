using System.Buffers;
using System.Text;
using Fetchle.Core.Storage;
using Fetchle.Core.Walking;

namespace Fetchle.Core.Index;

// path questions answered from the parent column: which entry is this path, and what's this entry's path
static class SnapshotPaths
{
    // deeper than any real tree; a chain longer than this means the snapshot lied, and validation forbids that
    public const int MAX_DEPTH = 4096;

    // the entry for a directory path at or under one of the snapshot's roots, or -1
    public static int FindDirectory(Snapshot snapshot, string path)
    {
        for (var r = 0; r < snapshot.RootCount; r++)
        {
            var root = snapshot.RootPath(r);
            if (!RootsConfig.Covers(root, path))
            {
                continue;
            }

            var entry = r;
            var rest = path.AsSpan(root.Length);
            foreach (var range in rest.SplitAny(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            {
                var component = rest[range];
                if (component.IsEmpty)
                {
                    continue;
                }

                entry = FindChild(snapshot, entry, component);
                if (entry < 0 || !snapshot.IsDirectory(entry))
                {
                    return -1;
                }
            }
            return entry;
        }
        return -1;
    }

    // files anywhere under a dir. one forward pass: an entry is inside when its parent is the dir or inside
    public static int CountFiles(Snapshot snapshot, int dir)
    {
        var parents = snapshot.Parents;
        var inside = new bool[parents.Length];
        inside[dir] = true;
        var files = 0;
        for (var i = Math.Max(dir + 1, snapshot.RootCount); i < parents.Length; i++)
        {
            if (inside[parents[i]])
            {
                inside[i] = true;
                files += snapshot.IsDirectory(i) ? 0 : 1;
            }
        }
        return files;
    }

    // children of a dir are one contiguous run because parents never decrease, so two binary searches find them
    public static (int Start, int End) Children(Snapshot snapshot, int parent)
    {
        var parents = snapshot.Parents;
        var start = LowerBound(parents, snapshot.RootCount, (uint)parent);
        var end = LowerBound(parents, start, (uint)parent + 1);
        return (start, end);
    }

    static int FindChild(Snapshot snapshot, int parent, ReadOnlySpan<char> name)
    {
        var (start, end) = Children(snapshot, parent);
        var names = snapshot.Names;
        Span<char> buffer = stackalloc char[512];
        var folded = -1;
        for (var i = start; i < end; i++)
        {
            var bytes = snapshot.Segment(names[i]);
            if (bytes.Length > buffer.Length)
            {
                buffer = new char[bytes.Length];
            }

            var chars = buffer[..Encoding.UTF8.GetChars(bytes, buffer)];
            if (chars.SequenceEqual(name))
            {
                return i;
            }

            if (folded < 0 && chars.Equals(name, PathComparison.Default))
            {
                folded = i;
            }
        }

        // an exact spelling wins, so on a case-sensitive volume (possible on macos) Foo never resolves to its sibling foo
        return folded;
    }

    static int LowerBound(ReadOnlySpan<uint> parents, int from, uint value)
    {
        int low = from, high = parents.Length;
        while (low < high)
        {
            var mid = (low + high) >>> 1;
            if (parents[mid] < value)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }
        return low;
    }

    // chars in the path of entry below scope, written as Path.Join would: prefix + separator + names
    public static int PathLength(Snapshot snapshot, int entry, int scope, int prefixLength, bool prefixEndsInSeparator)
    {
        var length = prefixLength;
        var names = snapshot.Names;
        var parents = snapshot.Parents;
        var separators = 0;
        for (var at = entry; at != scope; at = (int)parents[at])
        {
            length += CharCount(snapshot, names[at]);
            separators++;
        }

        return length + separators - (prefixEndsInSeparator ? 1 : 0);
    }

    // the full path of entry, spelled from the scope's path the way the walker spells it
    public static string FullPath(Snapshot snapshot, int entry, int scope, string prefix)
    {
        Span<int> chain = stackalloc int[64];
        int[]? rented = null;
        var depth = 0;
        var parents = snapshot.Parents;
        for (var at = entry; at != scope; at = (int)parents[at])
        {
            if (depth == chain.Length)
            {
                var bigger = ArrayPool<int>.Shared.Rent(chain.Length * 2);
                chain.CopyTo(bigger);
                if (rented is not null)
                {
                    ArrayPool<int>.Shared.Return(rented);
                }
                chain = rented = bigger;
            }
            chain[depth++] = at;
        }

        var builder = new StringBuilder(prefix, prefix.Length + depth * 16);
        var names = snapshot.Names;
        for (var d = depth - 1; d >= 0; d--)
        {
            if (builder.Length > 0 && builder[^1] != Path.DirectorySeparatorChar && builder[^1] != Path.AltDirectorySeparatorChar)
            {
                builder.Append(Path.DirectorySeparatorChar);
            }
            AppendName(builder, snapshot.Segment(names[chain[d]]));
        }

        if (rented is not null)
        {
            ArrayPool<int>.Shared.Return(rented);
        }
        return builder.ToString();
    }

    // the path below scope, separators included, the way the walker's ranker sees it. returns chars written, or -1 if it didn't fit
    public static int RelativePath(Snapshot snapshot, int entry, int scope, Span<char> buffer)
    {
        var names = snapshot.Names;
        var parents = snapshot.Parents;
        var end = buffer.Length;
        for (var at = entry; at != scope; at = (int)parents[at])
        {
            var bytes = snapshot.Segment(names[at]);
            var chars = Encoding.UTF8.GetCharCount(bytes);
            var needed = chars + (at == entry ? 0 : 1);
            if (needed > end)
            {
                return -1;
            }

            if (at != entry)
            {
                buffer[--end] = Path.DirectorySeparatorChar;
            }
            end -= chars;
            Encoding.UTF8.GetChars(bytes, buffer.Slice(end, chars));
        }

        var length = buffer.Length - end;
        buffer[end..].CopyTo(buffer);
        return length;
    }

    public static int CharCount(Snapshot snapshot, uint segment)
    {
        var bytes = snapshot.Segment(segment);
        return Ascii.IsValid(bytes) ? bytes.Length : Encoding.UTF8.GetCharCount(bytes);
    }

    static void AppendName(StringBuilder builder, ReadOnlySpan<byte> bytes)
    {
        Span<char> chars = stackalloc char[256];
        if (bytes.Length > chars.Length)
        {
            chars = new char[bytes.Length];
        }
        builder.Append(chars[..Encoding.UTF8.GetChars(bytes, chars)]);
    }
}
