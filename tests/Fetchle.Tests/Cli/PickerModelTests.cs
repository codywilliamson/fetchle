using Fetchle.Cli.Picker;
using Fetchle.Core.Search;

namespace Fetchle.Tests.Cli;

public class PickerModelTests
{
    static SearchDone Done(long sequence, int hits)
    {
        var list = Enumerable.Range(0, hits).Select(i => new SearchHit($"/f/{i}", hits - i, 1, DateTimeOffset.UnixEpoch)).ToList();
        return new SearchDone(sequence, "q", new SearchResult(list, hits, TimeSpan.Zero, null), null);
    }

    [Test]
    public async Task New_results_select_the_first_row()
    {
        var model = new PickerModel(3);
        model.Apply(Done(1, 5));
        model.Act(PickerAction.Down);
        model.Apply(Done(2, 5));

        await Assert.That(model.Selected).IsEqualTo(0);
        await Assert.That(model.SelectedPath).IsEqualTo("/f/0");
    }

    [Test]
    public async Task Down_scrolls_the_window_and_stops_at_the_last_hit()
    {
        var model = new PickerModel(3);
        model.Apply(Done(1, 5));
        for (var i = 0; i < 10; i++)
        {
            model.Act(PickerAction.Down);
        }

        await Assert.That(model.Selected).IsEqualTo(4);
        await Assert.That(model.Top).IsEqualTo(2);
    }

    [Test]
    public async Task Up_stops_at_the_first_hit()
    {
        var model = new PickerModel(3);
        model.Apply(Done(1, 5));
        model.Act(PickerAction.Up);

        await Assert.That(model.Selected).IsEqualTo(0);
    }

    [Test]
    public async Task Pages_move_one_window_at_a_time()
    {
        var model = new PickerModel(3);
        model.Apply(Done(1, 10));
        model.Act(PickerAction.PageDown);
        model.Act(PickerAction.PageDown);
        await Assert.That(model.Selected).IsEqualTo(6);

        model.Act(PickerAction.PageUp);
        await Assert.That(model.Selected).IsEqualTo(3);
        await Assert.That(model.Top).IsLessThanOrEqualTo(3);
    }

    [Test]
    public async Task Keys_with_no_hits_do_nothing()
    {
        var model = new PickerModel(3);
        model.Act(PickerAction.Down);

        await Assert.That(model.SelectedPath).IsNull();
    }

    [Test]
    public async Task An_older_result_never_replaces_a_newer_one()
    {
        var model = new PickerModel(3);
        model.Apply(Done(5, 2));

        await Assert.That(model.Apply(Done(4, 7))).IsFalse();
        await Assert.That(model.Hits.Count).IsEqualTo(2);
    }

    [Test]
    public async Task An_empty_query_result_clears_the_rows()
    {
        var model = new PickerModel(3);
        model.Apply(Done(1, 4));
        model.Apply(new SearchDone(2, "", null, null));

        await Assert.That(model.Hits).IsEmpty();
        await Assert.That(model.SelectedPath).IsNull();
    }
}
