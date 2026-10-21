using Fetchle.Core.Search;

namespace Fetchle.Cli.Picker;

// what a finished (and still current) search hands the picker. result is null for an empty query, error for a search that threw
sealed record SearchDone(long Sequence, string Query, SearchResult? Result, string? Error);

// one search at a time on a pool thread, newest keystroke wins. a submit cancels the running search and replaces whatever
// was waiting, and a finished search is only published when nothing newer was submitted meanwhile, so the screen never
// goes back to an older query. searches never overlap, which keeps the single shared session single-reader
sealed class LatestWinsSearch(Func<string, CancellationToken, SearchResult> search, Action<SearchDone> publish)
{
    readonly Lock _gate = new();
    string? _pending;
    long _pendingSequence;
    long _sequence;
    CancellationTokenSource? _running;
    bool _busy;

    public long LastSubmitted => Volatile.Read(ref _sequence);

    public long Submit(string query)
    {
        var blank = string.IsNullOrWhiteSpace(query);
        long sequence;
        lock (_gate)
        {
            sequence = ++_sequence;
            _running?.Cancel();
            _pending = blank ? null : query;
            _pendingSequence = sequence;
            if (!blank && !_busy)
            {
                _busy = true;
                ThreadPool.UnsafeQueueUserWorkItem(static self => self.Loop(), this, preferLocal: false);
            }
        }

        if (blank)
        {
            publish(new SearchDone(sequence, query, null, null));
        }
        return sequence;
    }

    void Loop()
    {
        while (true)
        {
            string query;
            long sequence;
            CancellationTokenSource cts;
            lock (_gate)
            {
                if (_pending is null)
                {
                    _busy = false;
                    return;
                }
                query = _pending;
                sequence = _pendingSequence;
                _pending = null;
                cts = _running = new CancellationTokenSource();
            }

            SearchDone? done;
            try
            {
                done = new SearchDone(sequence, query, search(query, cts.Token), null);
            }
            catch (OperationCanceledException)
            {
                done = null;
            }
            catch (Exception e)
            {
                done = new SearchDone(sequence, query, null, e.Message);
            }

            bool current;
            lock (_gate)
            {
                current = sequence == _sequence;
                _running = null;
            }
            cts.Dispose();

            if (done is not null && current)
            {
                publish(done);
            }
        }
    }
}
