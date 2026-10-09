namespace Fetchle.Core.Walking;

// one worker's pile of dirs. the owner pushes and takes at the end (depth-first, stays small),
// thieves take the front (oldest, nearest the root, so one steal carries lots of work).
// a power-of-two ring of structs so a push allocates nothing except when it doubles the array
sealed class WorkDeque
{
    const int START_CAPACITY = 16;

    readonly Lock _lock = new();
    DirTask[] _ring = new DirTask[START_CAPACITY];
    int _head;
    int _count;

    public void PushEnd(DirTask dir)
    {
        lock (_lock)
        {
            if (_count == _ring.Length)
            {
                Grow();
            }

            _ring[(_head + _count) & (_ring.Length - 1)] = dir;
            _count++;
        }
    }

    public bool TryTakeEnd(out DirTask dir)
    {
        lock (_lock)
        {
            if (_count == 0)
            {
                dir = default;
                return false;
            }

            var slot = (_head + _count - 1) & (_ring.Length - 1);
            dir = _ring[slot];
            _ring[slot] = default;
            _count--;
            return true;
        }
    }

    public bool TryStealFront(out DirTask dir)
    {
        lock (_lock)
        {
            if (_count == 0)
            {
                dir = default;
                return false;
            }

            dir = _ring[_head];
            _ring[_head] = default;
            _head = (_head + 1) & (_ring.Length - 1);
            _count--;
            return true;
        }
    }

    // unwraps into the front of a bigger array
    void Grow()
    {
        var bigger = new DirTask[_ring.Length * 2];
        var tail = _ring.Length - _head;
        Array.Copy(_ring, _head, bigger, 0, tail);
        Array.Copy(_ring, 0, bigger, tail, _head);
        _ring = bigger;
        _head = 0;
    }
}
