using Fetchle.Core.Embedding;

namespace Fetchle.Tests.Core.Embedding;

// the pipeline on the hand-made model (rules) and on the real one (ids pinned against HF tokenizers)
public class TokenizerTests
{
    [Test]
    public async Task A_word_splits_into_longest_match_then_prefixed_pieces()
    {
        using var model = TinyModel.Open();

        await Assert.That(model.TokenIds("playing")).IsEquivalentTo(new[] { TinyModel.PLAYING });
        await Assert.That(model.TokenIds("played")).IsEquivalentTo(new[] { TinyModel.PLAY, TinyModel.ED });
    }

    [Test]
    public async Task A_word_with_one_missing_piece_is_dropped_whole()
    {
        using var model = TinyModel.Open();

        await Assert.That(model.TokenIds("playx")).IsEmpty();
        await Assert.That(model.TokenIds("playx cafe")).IsEquivalentTo(new[] { TinyModel.CAFE });
    }

    [Test]
    public async Task Case_and_whitespace_do_not_matter()
    {
        using var model = TinyModel.Open();

        await Assert.That(model.TokenIds("  CAFE\t\nCafe cAfE ")).IsEquivalentTo(new[] { TinyModel.CAFE, TinyModel.CAFE, TinyModel.CAFE });
    }

    [Test]
    public async Task Every_punctuation_char_is_its_own_word()
    {
        using var model = TinyModel.Open();

        await Assert.That(model.TokenIds("cafe.cafe-.")).IsEquivalentTo(new[] { TinyModel.CAFE, TinyModel.DOT, TinyModel.CAFE, TinyModel.DASH, TinyModel.DOT });
        await Assert.That(model.TokenIds("cafe..")).IsEquivalentTo(new[] { TinyModel.CAFE, TinyModel.DOT, TinyModel.DOT });
    }

    [Test]
    public async Task Accents_are_stripped_before_lookup()
    {
        using var model = TinyModel.Open();

        await Assert.That(model.TokenIds("Café")).IsEquivalentTo(new[] { TinyModel.CAFE });
        await Assert.That(model.TokenIds("café")).IsEquivalentTo(new[] { TinyModel.CAFE });
        await Assert.That(model.TokenIds("É È Ê")).IsEquivalentTo(new[] { TinyModel.E, TinyModel.E, TinyModel.E });
    }

    [Test]
    public async Task Each_cjk_char_is_its_own_word()
    {
        using var model = TinyModel.Open();

        await Assert.That(model.TokenIds("日本")).IsEquivalentTo(new[] { TinyModel.RI, TinyModel.BEN });
        await Assert.That(model.TokenIds("cafe日cafe")).IsEquivalentTo(new[] { TinyModel.CAFE, TinyModel.RI, TinyModel.CAFE });
    }

    [Test]
    public async Task Control_chars_vanish_and_do_not_split_words()
    {
        using var model = TinyModel.Open();

        await Assert.That(model.TokenIds("ca\u0000f​e�")).IsEquivalentTo(new[] { TinyModel.CAFE });
    }

    [Test]
    public async Task A_word_past_max_chars_is_dropped_whole()
    {
        using var model = TinyModel.Open(maxWordChars: 8);

        await Assert.That(model.TokenIds("cafe")).IsEquivalentTo(new[] { TinyModel.CAFE });
        await Assert.That(model.TokenIds("playingplayingplaying cafe")).IsEquivalentTo(new[] { TinyModel.CAFE });
    }

    [Test]
    public async Task Surrogate_pairs_and_lone_surrogates_do_not_throw()
    {
        using var model = TinyModel.Open();

        await Assert.That(model.TokenIds("😀 cafe \uD800 x")).IsEquivalentTo(new[] { TinyModel.CAFE });
    }

    [Test]
    [Arguments("Microsoft.WindowsTerminal_8wekyb3d8bbwe", new[] { 6519, 18, 2651, 2340, 21317, 1146, 41, 28, 7551, 3807, 1503, 28103, 1626, 9328, 7551 })]
    [Arguments("ConsoleHost_history.txt", new[] { 9128, 14012, 1108, 41, 1387, 18, 30437 })]
    [Arguments("claude.exe", new[] { 7155, 18, 29869 })]
    public async Task Real_vocab_tokenizes_like_hf(string text, int[] expected)
    {
        using var model = ModelFiles.OpenOrSkip();

        await Assert.That(model.TokenIds(text)).IsEquivalentTo(expected);
    }
}
