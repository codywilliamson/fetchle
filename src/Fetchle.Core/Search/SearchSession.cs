using System.Diagnostics;
using Fetchle.Core.Index;
using Fetchle.Core.Ranking;
using Fetchle.Core.Ranking.Semantic;
using Fetchle.Core.Walking;

namespace Fetchle.Core.Search;

// one opened index for any number of queries: the snapshot and its vectors stay mapped between searches, so the
// picker's per-keystroke search and a long-lived mcp server pay the open once. roots the snapshot covers rank
// from it, the rest are walked into a LiveTree and ranked the same way
public sealed class SearchSession : IDisposable
{
    readonly Snapshot? _snapshot;
    readonly SegmentVectors? _vectors;
    readonly ITextEncoder? _model;
    readonly PruneRules _prune;

    SearchSession(Snapshot? snapshot, SegmentVectors? vectors, ITextEncoder? model, PruneRules prune, string? semanticOff)
    {
        _snapshot = snapshot;
        _vectors = vectors;
        _model = model;
        _prune = prune;
        SemanticOff = semanticOff;
    }

    // why indexed roots rank lexically only, or null when the semantic retriever is on
    public string? SemanticOff { get; }

    public bool HasSnapshot => _snapshot is not null;

    // store null means walk everything. throws IndexUnreadableException when a snapshot exists but can't be trusted
    public static SearchSession Open(IndexStore? store, PruneRules prune, ITextEncoder? model)
    {
        var snapshot = store?.Open();
        if (snapshot is null || model is null)
        {
            return new SearchSession(snapshot, null, model, prune, model is null ? "no model file" : null);
        }

        var vectors = SegmentVectors.Open(store!.VectorsPath, snapshot, model.Checksum, out var problem);
        return new SearchSession(snapshot, vectors, model, prune, problem);
    }

    // prefix: the query's last word is still being typed, as in the picker
    public SearchResult Search(SearchRequest request, CancellationToken cancellationToken, bool prefix = false)
    {
        var start = Stopwatch.GetTimestamp();
        var deadline = Deadline.After(request.Budget, start);
        var terms = QueryTerms.Parse(request.Query, prefix);
        var roots = SearchRoots.Resolve(request.Roots);

        var scopes = new List<IndexScope>();
        var walked = new List<string>();
        foreach (var root in roots)
        {
            var entry = _snapshot is null ? -1 : SnapshotPaths.FindDirectory(_snapshot, root);
            if (entry < 0)
            {
                walked.Add(root);
            }
            else
            {
                scopes.Add(new IndexScope(entry, root));
            }
        }

        var sources = new List<(Snapshot, RankedQuery.Outcome)>(2);
        var total = 0;
        var completed = true;
        if (scopes.Count > 0)
        {
            using var semantic = _vectors is null ? null : SemanticScores.Build(_snapshot!, _vectors, _model!, request.Query);
            var outcome = RankedQuery.Run(_snapshot!, request, terms, scopes, deadline, semantic);
            sources.Add((_snapshot!, outcome));
            total += outcome.Total;
            completed = outcome.Completed;
        }
        cancellationToken.ThrowIfCancellationRequested();

        using var live = completed && walked.Count > 0 ? LiveTree.Walk(walked, _prune, _model, deadline) : null;
        if (live is not null)
        {
            var liveScopes = new List<IndexScope>(walked.Count);
            for (var r = 0; r < walked.Count && r < live.Snapshot.RootCount; r++)
            {
                liveScopes.Add(new IndexScope(r, walked[r]));
            }

            using var semantic = live.Vectors is null ? null : SemanticScores.Build(live.Snapshot, live.Vectors, _model!, request.Query);
            var outcome = RankedQuery.Run(live.Snapshot, request, terms, liveScopes, deadline, semantic);
            sources.Add((live.Snapshot, outcome));
            total += outcome.Total;
            completed = live.Completed && outcome.Completed;
        }
        cancellationToken.ThrowIfCancellationRequested();

        var hits = Fusion.Fuse(sources, request.Limit);
        return new SearchResult(hits, total, Stopwatch.GetElapsedTime(start), completed ? null : StopReasons.BUDGET);
    }

    public IndexStatus GetStatus(IReadOnlyList<string> roots)
    {
        var builtAt = _snapshot?.Info().BuiltAt;
        var status = new List<RootStatus>(roots.Count);
        foreach (var root in roots)
        {
            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            // a subdir of an indexed root counts only its own subtree, not the whole root's files
            var entry = _snapshot is null ? -1 : SnapshotPaths.FindDirectory(_snapshot, full);
            status.Add(entry < 0 ? new RootStatus(full, null, null) : new RootStatus(full, SnapshotPaths.CountFiles(_snapshot!, entry), builtAt));
        }
        return new IndexStatus(status, VectorsComplete: _vectors is not null, WatcherLive: false);
    }

    public void Dispose()
    {
        _vectors?.Dispose();
        _snapshot?.Dispose();
    }
}
