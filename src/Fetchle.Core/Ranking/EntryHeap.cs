namespace Fetchle.Core.Ranking;

// the best `capacity` entries by score, as a min-heap of (score, entry). equal scores keep the lower entry id,
// which is the shallower or earlier-listed one, so results never depend on anything but the snapshot
sealed class EntryHeap(int capacity)
{
    readonly float[] _scores = new float[capacity];
    readonly int[] _entries = new int[capacity];

    public int Count { get; private set; }

    public bool WouldKeep(float score, int entry) =>
        Count < capacity || Beats(score, entry, _scores[0], _entries[0]);

    public void Add(float score, int entry)
    {
        if (Count < capacity)
        {
            _scores[Count] = score;
            _entries[Count] = entry;
            Up(Count++);
            return;
        }

        if (!Beats(score, entry, _scores[0], _entries[0]))
        {
            return;
        }

        _scores[0] = score;
        _entries[0] = entry;
        Down(0);
    }

    // best first
    public (float Score, int Entry)[] ToSorted()
    {
        var items = new (float Score, int Entry)[Count];
        for (var i = 0; i < Count; i++)
        {
            items[i] = (_scores[i], _entries[i]);
        }
        Array.Sort(items, (a, b) => Beats(a.Score, a.Entry, b.Score, b.Entry) ? -1 : Beats(b.Score, b.Entry, a.Score, a.Entry) ? 1 : 0);
        return items;
    }

    static bool Beats(float score, int entry, float otherScore, int otherEntry) =>
        score > otherScore || (score == otherScore && entry < otherEntry);

    void Up(int i)
    {
        while (i > 0)
        {
            var parent = (i - 1) / 2;
            if (!Beats(_scores[parent], _entries[parent], _scores[i], _entries[i]))
            {
                break;
            }
            Swap(i, parent);
            i = parent;
        }
    }

    void Down(int i)
    {
        while (true)
        {
            var left = 2 * i + 1;
            var smallest = i;
            if (left < Count && Beats(_scores[smallest], _entries[smallest], _scores[left], _entries[left]))
            {
                smallest = left;
            }

            if (left + 1 < Count && Beats(_scores[smallest], _entries[smallest], _scores[left + 1], _entries[left + 1]))
            {
                smallest = left + 1;
            }

            if (smallest == i)
            {
                return;
            }
            Swap(i, smallest);
            i = smallest;
        }
    }

    void Swap(int a, int b)
    {
        (_scores[a], _scores[b]) = (_scores[b], _scores[a]);
        (_entries[a], _entries[b]) = (_entries[b], _entries[a]);
    }
}
