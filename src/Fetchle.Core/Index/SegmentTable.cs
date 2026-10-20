namespace Fetchle.Core.Index;

// interned name segments: every unique name's chars in one arena, found again through an open-addressing table
// of ids. no string or dictionary node per name; a million unique names were 158 MB of garbage as strings
sealed class SegmentTable
{
    const int INITIAL_CAPACITY = 1024;
    const int EMPTY = -1;
    // grow at half full, so linear probes stay short
    const int LOAD_DIVISOR = 2;

    char[] _chars = new char[64 * 1024];
    int _charCount;
    int[] _starts = new int[INITIAL_CAPACITY];
    int[] _lengths = new int[INITIAL_CAPACITY];
    int[] _hashes = new int[INITIAL_CAPACITY];
    int[] _slots = NewSlots(INITIAL_CAPACITY * LOAD_DIVISOR);

    public int Count { get; private set; }

    public ReadOnlySpan<char> this[int id] => _chars.AsSpan(_starts[id], _lengths[id]);

    public int Intern(ReadOnlySpan<char> name)
    {
        var hash = string.GetHashCode(name);
        var mask = _slots.Length - 1;
        for (var slot = hash & mask; ; slot = (slot + 1) & mask)
        {
            var id = _slots[slot];
            if (id == EMPTY)
            {
                id = Add(name, hash);
                _slots[slot] = id;
                if (Count * LOAD_DIVISOR > _slots.Length)
                {
                    Rehash();
                }
                return id;
            }

            if (_hashes[id] == hash && this[id].SequenceEqual(name))
            {
                return id;
            }
        }
    }

    int Add(ReadOnlySpan<char> name, int hash)
    {
        if (Count == _starts.Length)
        {
            Array.Resize(ref _starts, Count * 2);
            Array.Resize(ref _lengths, Count * 2);
            Array.Resize(ref _hashes, Count * 2);
        }

        if (_charCount + name.Length > _chars.Length)
        {
            Array.Resize(ref _chars, Math.Max(_chars.Length * 2, _charCount + name.Length));
        }

        name.CopyTo(_chars.AsSpan(_charCount));
        _starts[Count] = _charCount;
        _lengths[Count] = name.Length;
        _hashes[Count] = hash;
        _charCount += name.Length;
        return Count++;
    }

    void Rehash()
    {
        _slots = NewSlots(_slots.Length * 2);
        var mask = _slots.Length - 1;
        for (var id = 0; id < Count; id++)
        {
            var slot = _hashes[id] & mask;
            while (_slots[slot] != EMPTY)
            {
                slot = (slot + 1) & mask;
            }
            _slots[slot] = id;
        }
    }

    static int[] NewSlots(int size)
    {
        var slots = new int[size];
        Array.Fill(slots, EMPTY);
        return slots;
    }
}
