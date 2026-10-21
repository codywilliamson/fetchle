using Fetchle.Core.Search;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Input;

namespace Fetchle.Cli.Picker;

// the live picker: an input box, a window of ranked rows, a footer. every keystroke submits the new query to the
// latest-wins runner from the ui loop itself, and a finished search is posted back to the loop to repaint
sealed class PickerApp
{
    public const int MAX_ROWS = 10;
    public const int MIN_ROWS = 3;
    // rows of chrome around the list: input line and footer
    const int CHROME_ROWS = 2;
    const string PROMPT = "> ";
    const string HINTS = "  up/down move, enter select, esc cancel";
    const string IDLE_FOOTER = "type to search";

    readonly PickerModel _model;
    readonly LatestWinsSearch _search;
    readonly TimeProvider _clock;
    readonly Action<long>? _onApplied;
    readonly State<string?> _query;
    readonly State<int> _frame = new(0);
    readonly TextBox _input;
    TerminalApp? _app;
    string? _submitted;
    string[] _words = [];
    bool _finished;
    string? _chosen;
    long _applied;

    public PickerApp(Func<string, CancellationToken, SearchResult> search, string initialQuery, int rows, TimeProvider clock, Action<long>? onApplied = null)
    {
        _model = new PickerModel(Math.Clamp(rows, MIN_ROWS, MAX_ROWS));
        _clock = clock;
        _onApplied = onApplied;
        _search = new LatestWinsSearch(search, Publish);
        _query = new State<string?>(initialQuery);
        _input = new TextBox(_query);
    }

    // rows that fit above the footer on a terminal this tall
    public static int RowsFor(int terminalRows) => Math.Clamp(terminalRows - CHROME_ROWS - 1, MIN_ROWS, MAX_ROWS);

    // the newest search result painted into the model, for tests and the latency bench to wait on
    public long AppliedSequence => Volatile.Read(ref _applied);
    public long LastSubmitted => _search.LastSubmitted;

    // blocks until Enter (returns the selected path) or Esc (returns null)
    public string? Run(TerminalInstance terminal)
    {
        var root = BuildVisual();
        terminal.Live(root, OnUpdate, new TerminalLiveOptions());
        return _chosen;
    }

    VStack BuildVisual()
    {
        var rows = new List<Visual>(_model.Rows + CHROME_ROWS)
        {
            new HStack(new TextBlock(PROMPT), _input),
        };
        for (var i = 0; i < _model.Rows; i++)
        {
            var row = i;
            rows.Add(new Markup(() => RowText(row)));
        }
        rows.Add(new Markup(FooterText));

        var stack = new VStack(rows.ToArray());
        Bind(stack, TerminalKey.Up, () => Act(PickerAction.Up));
        Bind(stack, TerminalKey.Down, () => Act(PickerAction.Down));
        Bind(stack, TerminalKey.PageUp, () => Act(PickerAction.PageUp));
        Bind(stack, TerminalKey.PageDown, () => Act(PickerAction.PageDown));
        Bind(stack, TerminalKey.Enter, Accept);
        Bind(stack, TerminalKey.Escape, Cancel);
        stack.AddKeyBinding(new KeyGesture('c', TerminalModifiers.Ctrl), Cancel);
        return stack;
    }

    static void Bind(Visual visual, TerminalKey key, Action action) =>
        visual.AddKeyBinding(new KeyGesture(key, TerminalModifiers.None), action);

    TerminalLoopResult OnUpdate(TerminalRunningContext ctx)
    {
        if (_app is null)
        {
            _app = ctx.App;
            // without this every keystroke is silently dropped
            ctx.App.Focus(_input);
            _input.CaretIndex = (_query.Value ?? "").Length;
        }

        var query = _query.Value ?? "";
        if (query != _submitted)
        {
            if (_submitted is not null || query.Length > 0)
            {
                _words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                _search.Submit(query);
            }
            _submitted = query;
        }

        return _finished ? TerminalLoopResult.Stop : TerminalLoopResult.Continue;
    }

    void Publish(SearchDone done) => _app!.Post(() =>
    {
        if (_model.Apply(done))
        {
            _onApplied?.Invoke(done.Sequence);
            Volatile.Write(ref _applied, done.Sequence);
            _frame.Value++;
        }
    });

    void Act(PickerAction action)
    {
        _model.Act(action);
        _frame.Value++;
    }

    void Accept()
    {
        if (_model.SelectedPath is { } path)
        {
            _chosen = path;
            _finished = true;
        }
    }

    void Cancel() => _finished = true;

    string RowText(int row)
    {
        _ = _frame.Value;
        var index = _model.Top + row;
        return index < _model.Hits.Count
            ? RowMarkup.Build(_model.Hits[index], _words, index == _model.Selected, _clock.GetUtcNow())
            : "";
    }

    string FooterText()
    {
        _ = _frame.Value;
        var text = _model.Error is { } error ? $"search failed: {error}"
            : _model.Result is { } result ? result.Footer() + HINTS
            : IDLE_FOOTER;
        return "[dim]" + XenoAtom.Ansi.AnsiMarkup.Escape(text) + "[/]";
    }
}
