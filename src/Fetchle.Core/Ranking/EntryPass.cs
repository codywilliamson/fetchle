using Fetchle.Core.Index;
using Fetchle.Core.Ranking.Lexical;
using Fetchle.Core.Ranking.Semantic;
using Fetchle.Core.Search;

namespace Fetchle.Core.Ranking;

// the per-entry half of a query: every entry under a scope, level by level (BfsLevels). a big level is split into
// chunks scanned in parallel, each by its own PassWorker with its own heaps; the per-entry arrays are shared, but each
// row is written by exactly one worker and only reads its parent, finished a level earlier. workers merge at the end
// with the heaps' total order, so the answer never depends on which thread did what
sealed class EntryPass
{
    public const byte SCOPE = 1;
    public const byte INSIDE = 2;
    // set on an entry at or below a scratch copy dir, see CopyMarks
    public const byte COPY = 4;

    // a level smaller than this scans on one thread; fanning out costs more than it saves
    const int PARALLEL_LEVEL = 32 * 1024;
    const int CHUNK = 16 * 1024;

    readonly PassWorker[] _workers;

    public EntryPass(Snapshot snapshot, SearchRequest request, IReadOnlyList<IndexScope> scopes, TermScores scores, SemanticScores? semantic,
        ulong[]? copies, byte[] state, byte[] depth, ulong[] carried, float[]? pathSums, bool wantsDirectory)
    {
        WantsDirectory = wantsDirectory;
        Snapshot = snapshot;
        Request = request;
        Scopes = scopes;
        Scores = scores;
        Semantic = semantic;
        Copies = copies;
        State = state;
        Depth = depth;
        Carried = carried;
        PathSums = pathSums;
        _workers = new PassWorker[Math.Max(1, Environment.ProcessorCount)];
        for (var w = 0; w < _workers.Length; w++)
        {
            _workers[w] = new PassWorker(this);
        }
    }

    public Snapshot Snapshot { get; }

    public SearchRequest Request { get; }

    public IReadOnlyList<IndexScope> Scopes { get; }

    public TermScores Scores { get; }

    public SemanticScores? Semantic { get; }

    public ulong[]? Copies { get; }

    public byte[] State { get; }

    public byte[] Depth { get; }

    public ulong[] Carried { get; }

    public float[]? PathSums { get; }

    public bool WantsDirectory { get; }

    public int Total { get; private set; }

    public bool Covered { get; private set; }

    // some path matched some query word, even if too weakly to be a lexical candidate
    public bool AnyWordMatched { get; private set; }

    // returns false when the deadline cut the pass short
    public bool Run(int first, Deadline deadline)
    {
        SeedScopes();
        foreach (var (start, end) in BfsLevels.Of(Snapshot.Parents, Snapshot.RootCount))
        {
            if (end <= first)
            {
                continue;
            }

            var from = Math.Max(start, first);
            if (deadline.IsExpired())
            {
                return Merge(false);
            }

            if (end - from < PARALLEL_LEVEL)
            {
                if (!ScanChunks(_workers[0], from, end, deadline))
                {
                    return Merge(false);
                }
                continue;
            }

            // each worker takes one contiguous run of chunks, so siblings mostly stay on one worker and its cache
            var chunks = (end - from + CHUNK - 1) / CHUNK;
            var perWorker = (chunks + _workers.Length - 1) / _workers.Length;
            var expired = 0;
            Parallel.For(0, _workers.Length, w =>
            {
                var runFrom = from + w * perWorker * CHUNK;
                var runTo = Math.Min(end, runFrom + perWorker * CHUNK);
                if (runFrom < runTo && !ScanChunks(_workers[w], runFrom, runTo, deadline))
                {
                    Interlocked.Exchange(ref expired, 1);
                }
            });

            if (expired != 0)
            {
                return Merge(false);
            }
        }
        return Merge(true);
    }

    // the deadline is a timestamp read, so it's checked once per chunk, not per row
    static bool ScanChunks(PassWorker worker, int from, int to, Deadline deadline)
    {
        for (var chunk = from; chunk < to; chunk += CHUNK)
        {
            if (deadline.IsExpired())
            {
                return false;
            }
            worker.Scan(chunk, Math.Min(to, chunk + CHUNK));
        }
        return true;
    }

    bool Merge(bool completed)
    {
        foreach (var worker in _workers)
        {
            Total += worker.Total;
            Covered |= worker.Covered;
            AnyWordMatched |= worker.AnyWordMatched;
        }
        return completed;
    }

    // each retriever's candidates across every worker, best first
    public List<RankedEntry> Ranked(bool lexical)
    {
        var merged = new EntryHeap(RankingWeights.CANDIDATES);
        foreach (var worker in _workers)
        {
            foreach (var (score, entry) in (lexical ? worker.Lexical : worker.Semantic).ToSorted())
            {
                merged.Add(score, entry);
            }
        }

        var sorted = merged.ToSorted();
        var ranked = new List<RankedEntry>(sorted.Length);
        foreach (var (score, entry) in sorted)
        {
            ranked.Add(new RankedEntry(entry, score, ScopeOf(entry)));
        }
        return ranked;
    }

    // a scope's path sum covers the ancestors above it too: centroids run over the whole path
    void SeedScopes()
    {
        if (Semantic is null)
        {
            return;
        }

        foreach (var scope in Scopes)
        {
            var sum = 0f;
            for (var at = scope.Entry; at >= Snapshot.RootCount; at = (int)Snapshot.Parents[at])
            {
                sum += Semantic.BySegment[Snapshot.Names[at]];
            }
            PathSums![scope.Entry] = sum;
        }
    }

    // the nearest ancestor marked SCOPE. scopes never nest, so there's exactly one
    public IndexScope ScopeOf(int entry)
    {
        if (Scopes.Count == 1)
        {
            return Scopes[0];
        }

        var parents = Snapshot.Parents;
        var at = (int)parents[entry];
        while ((State[at] & SCOPE) == 0)
        {
            at = (int)parents[at];
        }

        for (var s = 0; s < Scopes.Count; s++)
        {
            if (Scopes[s].Entry == at)
            {
                return Scopes[s];
            }
        }
        throw new InvalidOperationException("entry outside every scope");
    }
}
