namespace Fetchle.Core.Ranking;

// a snapshot's rows run roots first, then breadth-first, and parent ids never decrease. so each depth level is one
// contiguous range, and level L+1 is exactly the rows whose parent sits in level L: the next boundary is a binary
// search for the first row whose parent is past the current level. rows in one level only read their parents, which
// all sit in the level above, so a level can be scanned in parallel
static class BfsLevels
{
    // [start, end) per level below the roots, in order
    public static List<(int Start, int End)> Of(ReadOnlySpan<uint> parents, int roots)
    {
        var levels = new List<(int, int)>();
        var start = roots;
        var previousEnd = roots;
        while (start < parents.Length)
        {
            // the first row whose parent is at or past previousEnd starts the level after this one
            int low = start, high = parents.Length;
            while (low < high)
            {
                var mid = (low + high) >>> 1;
                if (parents[mid] < (uint)previousEnd)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid;
                }
            }

            // a level of zero rows can't happen in a valid snapshot (parents come before children); stop rather than spin
            if (low == start)
            {
                levels.Add((start, parents.Length));
                break;
            }

            levels.Add((start, low));
            previousEnd = low;
            start = low;
        }
        return levels;
    }
}
