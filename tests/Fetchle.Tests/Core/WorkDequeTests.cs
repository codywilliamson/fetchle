using TUnit.Assertions.Enums;
using Fetchle.Core.Walking;

namespace Fetchle.Tests.Core;

public class WorkDequeTests
{
    static DirTask Dir(int i) => new(i.ToString(), null);

    static WorkDeque Filled(int count)
    {
        var deque = new WorkDeque();
        for (var i = 0; i < count; i++)
        {
            deque.PushEnd(Dir(i));
        }

        return deque;
    }

    [Test]
    public async Task Empty_deque_has_nothing_to_take_or_steal()
    {
        var deque = new WorkDeque();

        await Assert.That(deque.TryTakeEnd(out _)).IsFalse();
        await Assert.That(deque.TryStealFront(out _)).IsFalse();
    }

    [Test]
    public async Task Take_end_is_newest_first()
    {
        var deque = Filled(3);

        await Assert.That(deque.TryTakeEnd(out var a)).IsTrue();
        await Assert.That(deque.TryTakeEnd(out var b)).IsTrue();
        await Assert.That(deque.TryTakeEnd(out var c)).IsTrue();
        await Assert.That(new[] { a.Path, b.Path, c.Path }).IsEquivalentTo(new[] { "2", "1", "0" }, CollectionOrdering.Matching);
        await Assert.That(deque.TryTakeEnd(out _)).IsFalse();
    }

    [Test]
    public async Task Steal_front_is_oldest_first()
    {
        var deque = Filled(3);

        await Assert.That(deque.TryStealFront(out var a)).IsTrue();
        await Assert.That(deque.TryStealFront(out var b)).IsTrue();
        await Assert.That(deque.TryStealFront(out var c)).IsTrue();
        await Assert.That(new[] { a.Path, b.Path, c.Path }).IsEquivalentTo(new[] { "0", "1", "2" }, CollectionOrdering.Matching);
        await Assert.That(deque.TryStealFront(out _)).IsFalse();
    }

    [Test]
    public async Task Take_end_and_steal_front_meet_in_the_middle()
    {
        var deque = Filled(4);

        deque.TryStealFront(out var front);
        deque.TryTakeEnd(out var end);
        deque.TryStealFront(out var second);
        deque.TryTakeEnd(out var third);

        await Assert.That(new[] { front.Path, end.Path, second.Path, third.Path }).IsEquivalentTo(new[] { "0", "3", "1", "2" }, CollectionOrdering.Matching);
        await Assert.That(deque.TryTakeEnd(out _)).IsFalse();
    }

    [Test]
    public async Task Keeps_order_when_the_ring_wraps()
    {
        var deque = Filled(6);
        for (var i = 0; i < 4; i++)
        {
            deque.TryStealFront(out _);
        }

        // head is now mid-buffer, these pushes wrap past the end of the array
        for (var i = 6; i < 12; i++)
        {
            deque.PushEnd(Dir(i));
        }

        var seen = new List<string>();
        while (deque.TryStealFront(out var dir))
        {
            seen.Add(dir.Path);
        }

        await Assert.That(seen).IsEquivalentTo(Enumerable.Range(4, 8).Select(i => i.ToString()), CollectionOrdering.Matching);
    }

    [Test]
    public async Task Grows_while_wrapped_without_losing_or_reordering()
    {
        var deque = Filled(8);
        for (var i = 0; i < 5; i++)
        {
            deque.TryStealFront(out _);
        }

        // wrapped and full-ish: pushing far past capacity forces several doublings mid-wrap
        for (var i = 8; i < 100; i++)
        {
            deque.PushEnd(Dir(i));
        }

        var seen = new List<string>();
        while (deque.TryStealFront(out var dir))
        {
            seen.Add(dir.Path);
        }

        await Assert.That(seen).IsEquivalentTo(Enumerable.Range(5, 95).Select(i => i.ToString()), CollectionOrdering.Matching);
    }

    [Test]
    public async Task Push_and_take_survive_many_cycles()
    {
        var deque = new WorkDeque();
        for (var i = 0; i < 1000; i++)
        {
            deque.PushEnd(Dir(i));
            deque.PushEnd(Dir(i + 1000));
            deque.TryStealFront(out _);
            deque.TryTakeEnd(out var dir);
            await Assert.That(dir.Path).IsEqualTo((i + 1000).ToString());
        }

        await Assert.That(deque.TryTakeEnd(out _)).IsFalse();
    }
}
