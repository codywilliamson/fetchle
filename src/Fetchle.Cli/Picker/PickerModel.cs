using Fetchle.Core.Search;

namespace Fetchle.Cli.Picker;

enum PickerAction { Up, Down, PageUp, PageDown }

// what the picker shows and which row is selected. no terminal in here, so keys and results unit-test directly
sealed class PickerModel(int rows)
{
    IReadOnlyList<SearchHit> _hits = [];

    public int Rows { get; } = rows;
    public IReadOnlyList<SearchHit> Hits => _hits;
    public int Selected { get; private set; }
    public int Top { get; private set; }
    public long AppliedSequence { get; private set; }
    public SearchResult? Result { get; private set; }
    public string? Error { get; private set; }

    public string? SelectedPath => Selected < _hits.Count ? _hits[Selected].Path : null;

    // false when the result is older than one already shown
    public bool Apply(SearchDone done)
    {
        if (done.Sequence <= AppliedSequence)
        {
            return false;
        }

        AppliedSequence = done.Sequence;
        Result = done.Result;
        Error = done.Error;
        _hits = done.Result?.Hits ?? [];
        Selected = 0;
        Top = 0;
        return true;
    }

    public void Act(PickerAction action)
    {
        switch (action)
        {
            case PickerAction.Up: Move(-1); break;
            case PickerAction.Down: Move(1); break;
            case PickerAction.PageUp: Move(-Rows); break;
            case PickerAction.PageDown: Move(Rows); break;
        }
    }

    void Move(int delta)
    {
        if (_hits.Count == 0)
        {
            return;
        }

        Selected = Math.Clamp(Selected + delta, 0, _hits.Count - 1);
        if (Selected < Top)
        {
            Top = Selected;
        }
        else if (Selected >= Top + Rows)
        {
            Top = Selected - Rows + 1;
        }
    }
}
