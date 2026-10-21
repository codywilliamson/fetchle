using Fetchle.Core.Ranking;

namespace Fetchle.Tests.Core.Ranking;

public class QueryTermsTests
{
    [Test]
    public async Task Stopwords_drop_out_but_a_query_of_only_stopwords_keeps_them()
    {
        await Assert.That(QueryTerms.Parse("rules for skipping during the walk").Terms).IsEquivalentTo(["rules", "skipping", "walk"]);
        await Assert.That(QueryTerms.Parse("the of").Terms).IsEquivalentTo(["the", "of"]);
    }

    [Test]
    public async Task Folder_words_become_intent()
    {
        var terms = QueryTerms.Parse("attest migrations folder");
        await Assert.That(terms.WantsDirectory).IsTrue();
        await Assert.That(terms.Terms).IsEquivalentTo(["attest", "migrations"]);
        await Assert.That(QueryTerms.Parse("folder").Terms).IsEquivalentTo(["folder"]);
    }

    [Test]
    public async Task The_word_being_typed_stays_even_when_it_looks_like_a_stopword()
    {
        var terms = QueryTerms.Parse("windows the", lastIsPrefix: true);
        await Assert.That(terms.Terms[^1]).IsEqualTo("the");
        await Assert.That(terms.LastIsPrefix).IsTrue();
    }

    [Test]
    public async Task Only_the_word_at_the_very_end_is_a_prefix()
    {
        await Assert.That(QueryTerms.Parse("windows term ", lastIsPrefix: true).LastIsPrefix).IsFalse();
        // a repeat: the last word typed is an earlier term, so the term that ends the list isn't being typed
        await Assert.That(QueryTerms.Parse("alpha beta alpha", lastIsPrefix: true).LastIsPrefix).IsFalse();
        // nine words: the ninth, the one being typed, never became a term
        await Assert.That(QueryTerms.Parse("a1 b2 c3 d4 e5 f6 g7 h8 i9", lastIsPrefix: true).LastIsPrefix).IsFalse();
    }

    [Test]
    public async Task A_megabyte_query_parses_without_blowing_the_stack()
    {
        var terms = QueryTerms.Parse(new string('x', 1_000_000));
        await Assert.That(terms.Terms.Single().Length).IsEqualTo(QueryTerms.MAX_QUERY_CHARS);
    }
}
