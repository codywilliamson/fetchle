using System.Diagnostics;
using System.Text;
using Fetchle.Core.Search;
using XenoAtom.Terminal;
using XenoAtom.Terminal.Backends;

namespace Fetchle.Cli.Picker;

// runs a PickerApp on an in-memory terminal and types into it. the tests and the keystroke latency bench use it, the
// shipped command never does. it owns the global Terminal while alive, so callers run one at a time
sealed class PickerDriver : IDisposable
{
    static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    readonly PickerApp _picker;
    readonly VirtualTerminalBackend _backend;
    readonly FrameWriter _screen;
    readonly TerminalSession _session;
    readonly Task<string?> _run;
    long _framesAtApply;

    public PickerDriver(
        Func<string, CancellationToken, SearchResult> search,
        string initialQuery,
        TimeProvider clock,
        int columns = 120,
        int rows = 24,
        bool captureText = false)
    {
        _screen = new FrameWriter(captureText);
        _backend = new VirtualTerminalBackend(_screen, _screen, new TerminalSize(columns, rows), null, false);
        _session = Terminal.Open(_backend, new TerminalOptions(), force: true);
        _picker = new PickerApp(search, initialQuery, PickerApp.RowsFor(rows), clock, _ => _framesAtApply = _screen.Frames);
        _run = Task.Run(() => _picker.Run(Terminal.Instance));
    }

    public Task<string?> Finished => _run.WaitAsync(WaitLimit);
    public bool IsFinished => _run.IsCompleted;
    public string ScreenText => _screen.Text;
    public long Frames => _screen.Frames;

    public void Press(TerminalKey key, TerminalModifiers modifiers = TerminalModifiers.None) =>
        _backend.PushEvent(new TerminalKeyEvent { Key = key, Modifiers = modifiers });

    public void Press(char c) => _backend.PushEvent(new TerminalTextEvent { Text = c.ToString() });

    public void Press(char c, TerminalModifiers modifiers) =>
        _backend.PushEvent(new TerminalKeyEvent { Key = TerminalKey.Unknown, Char = c, Modifiers = modifiers });

    public void Type(string text)
    {
        foreach (var c in text)
        {
            Press(c);
        }
    }

    // blocks until the query the picker holds has been searched and its rows are in a written frame
    public void WaitForSettled()
    {
        var limit = Stopwatch.StartNew();
        var spin = new SpinWait();
        while (_picker.AppliedSequence < _picker.LastSubmitted || _picker.AppliedSequence == 0)
        {
            Guard(limit, spin);
        }
        WaitForFrameAfterApply(limit, spin);
    }

    // presses one character and measures until the frame carrying its results is written
    public TimeSpan PressAndWaitForPaint(char c)
    {
        var limit = Stopwatch.StartNew();
        var spin = new SpinWait();
        var before = _picker.LastSubmitted;
        var start = Stopwatch.GetTimestamp();
        Press(c);
        while (_picker.LastSubmitted == before)
        {
            Guard(limit, spin);
        }

        var target = _picker.LastSubmitted;
        while (_picker.AppliedSequence < target)
        {
            Guard(limit, spin);
        }
        WaitForFrameAfterApply(limit, spin);
        return Stopwatch.GetElapsedTime(start, _screen.LastFrameEnd);
    }

    void WaitForFrameAfterApply(Stopwatch limit, SpinWait spin)
    {
        while (_screen.Frames <= Volatile.Read(ref _framesAtApply))
        {
            Guard(limit, spin);
        }
    }

    void Guard(Stopwatch limit, SpinWait spin)
    {
        if (limit.Elapsed > WaitLimit)
        {
            throw new TimeoutException($"the picker never painted. applied {_picker.AppliedSequence} of {_picker.LastSubmitted}, {_screen.Frames} frames");
        }
        spin.SpinOnce(-1);
    }

    public void Dispose()
    {
        if (!_run.IsCompleted)
        {
            Press(TerminalKey.Escape);
            _run.Wait(WaitLimit);
        }
        _session.Dispose();
    }

    // counts finished frames (DEC 2026 end marker) and stamps the moment the last one was written
    sealed class FrameWriter(bool capture) : TextWriter
    {
        const string FRAME_END = "\e[?2026l";

        readonly StringBuilder _text = new();
        long _frames;
        long _lastFrameEnd;

        public override Encoding Encoding => Encoding.UTF8;
        public long Frames => Volatile.Read(ref _frames);
        public long LastFrameEnd => Volatile.Read(ref _lastFrameEnd);

        public string Text
        {
            get
            {
                lock (_text)
                {
                    return _text.ToString();
                }
            }
        }

        public override void Write(char value) => Track([value]);
        public override void Write(string? value) => Track(value);
        public override void Write(char[] buffer, int index, int count) => Track(buffer.AsSpan(index, count));
        public override void Write(ReadOnlySpan<char> buffer) => Track(buffer);

        void Track(ReadOnlySpan<char> chunk)
        {
            if (capture)
            {
                lock (_text)
                {
                    _text.Append(chunk);
                }
            }

            if (chunk.Contains(FRAME_END, StringComparison.Ordinal))
            {
                Volatile.Write(ref _lastFrameEnd, Stopwatch.GetTimestamp());
                Interlocked.Increment(ref _frames);
            }
        }
    }
}
