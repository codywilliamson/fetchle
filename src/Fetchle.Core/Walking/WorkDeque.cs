using System.Diagnostics.CodeAnalysis;

namespace Fetchle.Core.Walking;

// one worker's pile of dirs. the owner pushes and takes at the end (depth-first, stays small),
// thieves take the front (oldest, nearest the root, so one steal carries lots of work)
sealed class WorkDeque
{
    readonly LinkedList<string> _dirs = new();
    readonly Lock _lock = new();

    public void PushEnd(string dir)
    {
        lock (_lock)
        {
            _dirs.AddLast(dir);
        }
    }

    public bool TryTakeEnd([NotNullWhen(true)] out string? dir)
    {
        lock (_lock)
        {
            return TryRemove(_dirs.Last, out dir);
        }
    }

    public bool TryStealFront([NotNullWhen(true)] out string? dir)
    {
        lock (_lock)
        {
            return TryRemove(_dirs.First, out dir);
        }
    }

    bool TryRemove(LinkedListNode<string>? node, [NotNullWhen(true)] out string? dir)
    {
        if (node is null)
        {
            dir = null;
            return false;
        }

        dir = node.Value;
        _dirs.Remove(node);
        return true;
    }
}
