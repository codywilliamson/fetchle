using Fetchle.Core.Naive;
using Fetchle.Core.Search;

namespace Fetchle.Tests.Core;

public class TopHitsTests
{
    static SearchHit Hit(string path, double score) => new(path, score, 0, DateTimeOffset.UnixEpoch);

    static List<string> Offer(int limit, params SearchHit[] hits)
    {
        var top = new TopHits(limit);
        foreach (var hit in hits)
        {
            if (top.WouldKeep(hit.Score, hit.Path.Length))
            {
                top.Add(hit);
            }
        }

        return top.ToSortedList().ConvertAll(h => h.Path);
    }

    [Test]
    public async Task Keeps_only_the_best_limit_in_order() =>
        await Assert.That(Offer(2, Hit("/aaaa", 0.5), Hit("/bbbbbb", 1), Hit("/cc", 0.5), Hit("/dddd", 1)))
            .IsEquivalentTo(["/dddd", "/bbbbbb"]);

    [Test]
    public async Task Ties_break_on_shorter_then_ordinal_path() =>
        await Assert.That(Offer(3, Hit("/b", 1), Hit("/a", 1), Hit("/zz", 1), Hit("/c", 1)))
            .IsEquivalentTo(["/a", "/b", "/c"]);

    [Test]
    public async Task Rejects_worse_hits_without_building_them()
    {
        var top = new TopHits(1);
        top.Add(Hit("/a", 1));
        await Assert.That(top.WouldKeep(0.5, 1)).IsFalse();
        await Assert.That(top.WouldKeep(1, 5)).IsFalse();
        await Assert.That(top.WouldKeep(1, 2)).IsTrue();
    }
}
