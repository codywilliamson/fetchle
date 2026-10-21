using Fetchle.Core.Index;
using Fetchle.Core.Search;
using Fetchle.Core.Walking;
using Fetchle.Tests.Core.Index;

namespace Fetchle.Tests.Core.Ranking;

public class SemanticCacheTests
{
    // counts encodes, so a test can see when the session reused its semantic scores
    sealed class CountingEncoder : Fetchle.Core.Ranking.Semantic.ITextEncoder
    {
        readonly FakeEncoder _inner = new();

        public int Encodes { get; private set; }

        public ulong Checksum => _inner.Checksum;

        public int Dimensions => _inner.Dimensions;

        public ReadOnlySpan<float> Mean => _inner.Mean;

        public bool Encode(ReadOnlySpan<char> text, Span<float> vector)
        {
            Encodes++;
            return _inner.Encode(text, vector);
        }
    }

    [Test]
    public async Task Keystrokes_inside_a_word_reuse_the_semantic_scores()
    {
        using var tree = Fetchle.Fixtures.FixtureTree.Create(fillerFiles: 50);
        using var index = new TempIndex();
        var encoder = new CountingEncoder();
        index.Store.Rebuild([tree.Root], PruneRules.Default, encoder, CancellationToken.None);
        var afterRebuild = encoder.Encodes;

        using var session = SearchSession.Open(index.Store, PruneRules.Default, encoder);
        foreach (var typed in new[] { "windows t", "windows te", "windows ter", "windows term" })
        {
            session.Search(new SearchRequest(typed, [tree.Root], 5, TimeSpan.FromSeconds(10)), CancellationToken.None, prefix: true);
        }

        // "windows " is the only finished text across all four, so it embedded once
        await Assert.That(encoder.Encodes - afterRebuild).IsEqualTo(1);
    }
}
