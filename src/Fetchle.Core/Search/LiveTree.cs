using Fetchle.Core.Index;
using Fetchle.Core.Ranking.Semantic;
using Fetchle.Core.Walking;

namespace Fetchle.Core.Search;

// a root nobody indexed, walked into an in-memory snapshot so it ranks exactly like an indexed one.
// it gets vectors too when the model is there and the tree is small enough to embed inside a query
sealed class LiveTree : IDisposable
{
    // segments embedded on the fly before semantic is skipped for a live walk
    public const int MAX_LIVE_EMBED_SEGMENTS = 50_000;

    LiveTree(Snapshot snapshot, SegmentVectors? vectors, bool completed)
    {
        Snapshot = snapshot;
        Vectors = vectors;
        Completed = completed;
    }

    public Snapshot Snapshot { get; }

    public SegmentVectors? Vectors { get; }

    // false when the deadline cut the walk short
    public bool Completed { get; }

    public static LiveTree Walk(IReadOnlyList<string> roots, PruneRules prune, ITextEncoder? model, Deadline deadline)
    {
        var walks = new List<RootWalk>(roots.Count);
        var completed = true;
        foreach (var root in roots)
        {
            var (walk, done) = IndexStore.WalkRoot(root, prune, deadline);
            walks.Add(walk);
            completed &= done;
            if (!completed)
            {
                break;
            }
        }

        var image = SnapshotWriter.ToBytes(TreeAssembler.Assemble(walks), DateTimeOffset.UtcNow);
        var snapshot = Snapshot.FromWrittenImage(image, "live walk");
        SegmentVectors? vectors = null;
        if (model is not null && snapshot.SegmentCount <= MAX_LIVE_EMBED_SEGMENTS && !deadline.IsExpired())
        {
            var parts = VectorBuilder.Build(snapshot, model);
            vectors = SegmentVectors.FromBytes(VectorWriter.ToBytes(parts, snapshot.Checksum, model.Checksum), snapshot, model.Checksum);
        }
        return new LiveTree(snapshot, vectors, completed);
    }

    public void Dispose()
    {
        Vectors?.Dispose();
        Snapshot.Dispose();
    }
}
