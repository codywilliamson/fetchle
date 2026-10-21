using Fetchle.Cli.Picker;
using Fetchle.Core.Search;

namespace Fetchle.Tests.Cli;

public class PickerRowMarkupTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    static SearchHit Hit(string path) => new(path, 1, 1_536, Now.AddDays(-2));

    [Test]
    public async Task Each_typed_word_is_highlighted_where_it_last_occurs()
    {
        var markup = RowMarkup.Build(Hit("/h/app/settings.json"), ["settings", "app"], selected: false, Now);

        await Assert.That(markup).Contains("/h/[bold yellow]app[/]/[bold yellow]settings[/].json");
        await Assert.That(markup).Contains("[dim]  1.5 KB  2d ago[/]");
    }

    [Test]
    public async Task Overlapping_words_merge_into_one_highlight()
    {
        await Assert.That(RowMarkup.Spans("/a/settings", ["sett", "tings"])).IsEquivalentTo([(3, 11)]);
    }

    [Test]
    public async Task Selected_row_gets_a_marker()
    {
        await Assert.That(RowMarkup.Build(Hit("/a"), [], selected: true, Now)).StartsWith("[bold]> [/]");
        await Assert.That(RowMarkup.Build(Hit("/a"), [], selected: false, Now)).StartsWith("  /a");
    }

    [Test]
    public async Task Brackets_in_a_path_cannot_turn_into_markup()
    {
        var markup = RowMarkup.Build(Hit("/a/[red]x.txt"), [], selected: false, Now);

        await Assert.That(markup).DoesNotContain("[red]x");
    }
}
