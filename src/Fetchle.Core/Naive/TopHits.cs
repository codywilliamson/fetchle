using Fetchle.Core.Search;

namespace Fetchle.Core.Naive;

// PLACEHOLDER: better = higher score, then shorter path, then ordinal path
public sealed class TopHits(int limit)
{
    // min-heap on "better", so the root is the worst hit kept
    readonly PriorityQueue<SearchHit, SearchHit> _heap = new(Comparer<SearchHit>.Create(Compare));

    // the caller can skip building a hit (and its path string) when this is false
    public bool WouldKeep(double score, int pathLength)
    {
        if (_heap.Count < limit)
        {
            return true;
        }

        var worst = _heap.Peek();
        return score > worst.Score || (score == worst.Score && pathLength <= worst.Path.Length);
    }

    // only call after WouldKeep returned true
    public void Add(SearchHit hit)
    {
        if (_heap.Count < limit)
        {
            _heap.Enqueue(hit, hit);
        }
        else if (Compare(hit, _heap.Peek()) > 0)
        {
            _heap.DequeueEnqueue(hit, hit);
        }
    }

    public List<SearchHit> ToSortedList()
    {
        var hits = new List<SearchHit>(_heap.Count);
        foreach (var (hit, _) in _heap.UnorderedItems)
        {
            hits.Add(hit);
        }

        hits.Sort((a, b) => Compare(b, a));
        return hits;
    }

    // > 0 when a is better than b
    static int Compare(SearchHit a, SearchHit b)
    {
        var c = a.Score.CompareTo(b.Score);
        if (c == 0)
        {
            c = b.Path.Length.CompareTo(a.Path.Length);
        }

        return c != 0 ? c : string.CompareOrdinal(b.Path, a.Path);
    }
}
