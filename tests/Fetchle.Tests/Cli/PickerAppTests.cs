using Fetchle.Cli.Picker;
using Fetchle.Core.Search;
using XenoAtom.Terminal;

namespace Fetchle.Tests.Cli;

// the whole picker (ui loop, runner, model) on an in-memory terminal with injected keys. there is no real tty on a ci
// runner, so this is the terminal-level coverage; the e2e suite covers the exe's refusal to run without one
[NotInParallel(nameof(Terminal))]
public class PickerAppTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
    static readonly string[] Paths = ["/h/notes/settings.md", "/h/app/settings.json", "/h/app/readme.md", "/h/app/Settings/theme.json"];

    // every path containing every word, in a fixed order
    static SearchResult Fake(string query, CancellationToken _)
    {
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var hits = Paths
            .Where(p => words.All(w => p.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .Select((p, i) => new SearchHit(p, 10 - i, 1_536, Now.AddDays(-2)))
            .ToList();
        return new SearchResult(hits, hits.Count, TimeSpan.FromMilliseconds(1), null);
    }

    static PickerDriver Start(string query = "") => new(Fake, query, new FixedClock(Now), captureText: true);

    [Test]
    public async Task Typing_ranks_and_enter_returns_the_selected_path()
    {
        using var picker = Start();
        picker.Type("settings");
        picker.WaitForSettled();
        picker.Press(TerminalKey.Down);
        picker.Press(TerminalKey.Enter);

        await Assert.That(await picker.Finished).IsEqualTo("/h/app/settings.json");
        await Assert.That(picker.ScreenText).Contains("3 matches, showing 3");
    }

    [Test]
    public async Task Initial_query_is_searched_without_a_keystroke()
    {
        using var picker = Start("readme");
        picker.WaitForSettled();
        picker.Press(TerminalKey.Enter);

        await Assert.That(await picker.Finished).IsEqualTo("/h/app/readme.md");
    }

    [Test]
    public async Task Escape_cancels_with_no_path()
    {
        using var picker = Start("settings");
        picker.WaitForSettled();
        picker.Press(TerminalKey.Escape);

        await Assert.That(await picker.Finished).IsNull();
    }

    [Test]
    public async Task Ctrl_c_cancels_with_no_path()
    {
        using var picker = Start("settings");
        picker.WaitForSettled();
        picker.Press('c', TerminalModifiers.Ctrl);

        await Assert.That(await picker.Finished).IsNull();
    }

    [Test]
    public async Task Enter_with_no_matches_stays_open()
    {
        using var picker = Start("zzz");
        picker.WaitForSettled();
        picker.Press(TerminalKey.Enter);
        await Task.Delay(100);

        await Assert.That(picker.IsFinished).IsFalse();
    }

    [Test]
    public async Task Empty_query_shows_nothing_and_searches_nothing()
    {
        var searched = 0;
        using var picker = new PickerDriver((q, ct) => { Interlocked.Increment(ref searched); return Fake(q, ct); }, "", new FixedClock(Now), captureText: true);
        picker.Type("a");
        picker.WaitForSettled();
        picker.Press(TerminalKey.Backspace);
        picker.WaitForSettled();

        await Assert.That(searched).IsEqualTo(1);
        await Assert.That(picker.ScreenText).Contains("type to search");
    }
}
