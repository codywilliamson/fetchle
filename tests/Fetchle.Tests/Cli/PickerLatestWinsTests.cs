using Fetchle.Cli.Picker;
using Fetchle.Core.Search;

namespace Fetchle.Tests.Cli;

public class PickerLatestWinsTests
{
    static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    static SearchResult Result(string query) =>
        new([new SearchHit("/" + query, 1, 1, DateTimeOffset.UnixEpoch)], 1, TimeSpan.Zero, null);

    sealed class Published
    {
        readonly List<SearchDone> _done = [];
        readonly SemaphoreSlim _signal = new(0);

        public void Add(SearchDone done)
        {
            lock (_done)
            {
                _done.Add(done);
            }
            _signal.Release();
        }

        public async Task<SearchDone> Next()
        {
            await _signal.WaitAsync(Limit);
            lock (_done)
            {
                return _done[^1];
            }
        }

        public string[] Queries()
        {
            lock (_done)
            {
                return [.. _done.Select(d => d.Query)];
            }
        }
    }

    [Test]
    public async Task A_newer_keystroke_cancels_the_running_search_and_only_its_result_shows()
    {
        var published = new Published();
        var started = new SemaphoreSlim(0);
        var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new LatestWinsSearch((query, ct) =>
        {
            if (query != "ab")
            {
                started.Release();
                // a slow search that notices its token
                try
                {
                    Task.Delay(Limit, ct).Wait(CancellationToken.None);
                }
                catch (AggregateException e) when (e.InnerException is OperationCanceledException)
                {
                    cancelled.SetResult(true);
                    throw new OperationCanceledException(ct);
                }
            }
            return Result(query);
        }, published.Add);

        runner.Submit("a");
        await started.WaitAsync(Limit);
        var newest = runner.Submit("ab");

        var done = await published.Next();
        await Assert.That(await cancelled.Task.WaitAsync(Limit)).IsTrue();
        await Assert.That(done.Query).IsEqualTo("ab");
        await Assert.That(done.Sequence).IsEqualTo(newest);
        await Assert.That(published.Queries()).IsEquivalentTo(["ab"]);
    }

    [Test]
    public async Task A_stale_search_that_ignores_cancellation_is_still_dropped()
    {
        var published = new Published();
        var started = new SemaphoreSlim(0);
        var release = new SemaphoreSlim(0);
        var runner = new LatestWinsSearch((query, _) =>
        {
            if (query == "a")
            {
                started.Release();
                release.Wait(Limit);
            }
            return Result(query);
        }, published.Add);

        runner.Submit("a");
        await started.WaitAsync(Limit);
        runner.Submit("ab");
        release.Release();

        var done = await published.Next();
        await Assert.That(done.Query).IsEqualTo("ab");
        await Assert.That(published.Queries()).IsEquivalentTo(["ab"]);
    }

    [Test]
    public async Task Typing_ahead_of_a_busy_search_runs_only_the_last_query()
    {
        var published = new Published();
        var started = new SemaphoreSlim(0);
        var release = new SemaphoreSlim(0);
        var ran = new List<string>();
        var runner = new LatestWinsSearch((query, _) =>
        {
            lock (ran)
            {
                ran.Add(query);
            }
            if (query == "a")
            {
                started.Release();
                release.Wait(Limit);
            }
            return Result(query);
        }, published.Add);

        runner.Submit("a");
        await started.WaitAsync(Limit);
        runner.Submit("ab");
        runner.Submit("abc");
        runner.Submit("abcd");
        release.Release();

        var done = await published.Next();
        await Assert.That(done.Query).IsEqualTo("abcd");
        string[] searched;
        lock (ran)
        {
            searched = [.. ran];
        }
        await Assert.That(searched).IsEquivalentTo(["a", "abcd"]);
    }

    [Test]
    public async Task An_empty_query_publishes_nothing_to_show_at_once_without_searching()
    {
        var published = new Published();
        var searched = 0;
        var runner = new LatestWinsSearch((query, _) => { Interlocked.Increment(ref searched); return Result(query); }, published.Add);

        var sequence = runner.Submit("  ");
        var done = await published.Next();

        await Assert.That(done.Sequence).IsEqualTo(sequence);
        await Assert.That(done.Result).IsNull();
        await Assert.That(searched).IsEqualTo(0);
    }

    [Test]
    public async Task Emptying_the_box_drops_a_search_still_in_flight()
    {
        var published = new Published();
        var started = new SemaphoreSlim(0);
        var release = new SemaphoreSlim(0);
        var runner = new LatestWinsSearch((query, _) =>
        {
            started.Release();
            release.Wait(Limit);
            return Result(query);
        }, published.Add);

        runner.Submit("a");
        await started.WaitAsync(Limit);
        runner.Submit("");
        release.Release();
        // let the worker finish and decide
        runner.Submit("z");
        var done = await published.Next();
        if (done.Query != "z")
        {
            done = await published.Next();
        }

        await Assert.That(published.Queries()).DoesNotContain("a");
    }

    [Test]
    public async Task A_search_that_throws_publishes_the_message_instead_of_crashing()
    {
        var published = new Published();
        var runner = new LatestWinsSearch((_, _) => throw new IOException("disk gone"), published.Add);

        runner.Submit("a");
        var done = await published.Next();

        await Assert.That(done.Error).IsEqualTo("disk gone");
        await Assert.That(done.Result).IsNull();
    }
}
