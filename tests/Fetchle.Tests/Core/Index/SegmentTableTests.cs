using Fetchle.Core.Index;

namespace Fetchle.Tests.Core.Index;

public class SegmentTableTests
{
    // a dictionary is the reference: same ids for the same names, through every rehash
    [Test]
    public async Task Interns_like_a_dictionary()
    {
        var rng = new Random(5);
        var table = new SegmentTable();
        var reference = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < 50_000; i++)
        {
            var name = rng.Next(4) == 0 ? "" : $"{(char)('a' + rng.Next(26))}{rng.Next(20_000)}{(rng.Next(2) == 0 ? "" : "é")}";
            var id = table.Intern(name);
            if (!reference.TryGetValue(name, out var expected))
            {
                expected = reference.Count;
                reference.Add(name, expected);
            }

            await Assert.That(id).IsEqualTo(expected);
        }

        await Assert.That(table.Count).IsEqualTo(reference.Count);
        foreach (var (name, id) in reference)
        {
            await Assert.That(table[id].ToString()).IsEqualTo(name);
        }
    }
}
