using Fetchle.Core.Index;
using Fetchle.Core.Ranking;
using Fetchle.Core.Ranking.Semantic;
using Fetchle.Core.Search;

namespace Fetchle.Tests.Core.Ranking;

// a model with a handful of concepts: every word maps to one concept's direction plus a little of its own
sealed class FakeEncoder : ITextEncoder
{
    static readonly Dictionary<string, int> Concepts = new()
    {
        ["spotify"] = 0, ["music"] = 0, ["streaming"] = 0, ["song"] = 0,
        ["terminal"] = 1, ["console"] = 1, ["shell"] = 1, ["windows"] = 1,
        ["settings"] = 2, ["config"] = 2, ["json"] = 2, ["preferences"] = 2,
        ["exe"] = 3, ["binary"] = 3, ["app"] = 3,
        ["roaming"] = 4, ["appdata"] = 4, ["local"] = 4,
    };

    readonly float[] _mean = new float[SignCodes.DIMENSIONS];

    public ulong Checksum => 42;

    public ReadOnlySpan<float> Mean => _mean;

    public bool Encode(ReadOnlySpan<char> text, Span<float> vector)
    {
        vector[..SignCodes.DIMENSIONS].Clear();
        var known = false;
        foreach (var word in text.ToString().Split(' '))
        {
            if (!Concepts.TryGetValue(word, out var concept))
            {
                continue;
            }

            known = true;
            var rng = new Random(concept);
            for (var d = 0; d < SignCodes.DIMENSIONS; d++)
            {
                vector[d] += (float)(rng.NextDouble() - 0.5);
            }

            var own = new Random(word.GetHashCode(StringComparison.Ordinal) & 0xFFFF);
            for (var d = 0; d < SignCodes.DIMENSIONS; d++)
            {
                vector[d] += 0.2f * (float)(own.NextDouble() - 0.5);
            }
        }

        if (!known)
        {
            return false;
        }

        var norm = MathF.Sqrt(Dot(vector, vector));
        for (var d = 0; d < SignCodes.DIMENSIONS; d++)
        {
            vector[d] /= norm;
        }
        return true;
    }

    public static float Dot(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        var sum = 0f;
        for (var d = 0; d < SignCodes.DIMENSIONS; d++)
        {
            sum += a[d] * b[d];
        }
        return sum;
    }
}

public class SemanticTests
{
    static readonly string Root = Path.Join(Path.GetTempPath(), "fetchle-semantic-never-created");

    static readonly string[] Files =
    [
        "AppData/Roaming/Spotify/Spotify.exe",
        "AppData/Local/Packages/Microsoft.WindowsTerminal_8wekyb3d8bbwe/LocalState/settings.json",
        "AppData/Local/FiveM/citizen/streaming_surrogate.rpf",
        "AppData/Roaming/Claude/claude-code/2.1.286/635c1867224a/claude.exe",
        "Documents/notes/todo.txt",
    ];

    static (Snapshot Snapshot, SegmentVectors Vectors) Build(FakeEncoder encoder)
    {
        var tree = TreeFromPaths.Build(Root, Files.Select(f => (f, false)));
        var snapshot = Snapshot.FromBytes(SnapshotWriter.ToBytes(tree, DateTimeOffset.UnixEpoch));
        var parts = VectorBuilder.Build(snapshot, encoder);
        var vectors = SegmentVectors.FromBytes(VectorWriter.ToBytes(parts, snapshot.Checksum, encoder.Checksum), snapshot, encoder.Checksum);
        return (snapshot, vectors);
    }

    static string[] Search(string query)
    {
        var encoder = new FakeEncoder();
        var (snapshot, vectors) = Build(encoder);
        using (snapshot)
        using (vectors)
        {
            using var semantic = SemanticScores.Build(snapshot, vectors, encoder, query);
            var request = new SearchRequest(query, [Root], 5, TimeSpan.FromSeconds(10), Type: EntryType.Files);
            var outcome = RankedQuery.Run(snapshot, request, QueryTerms.Parse(query), [new IndexScope(0, Root)],
                Deadline.After(request.Budget, System.Diagnostics.Stopwatch.GetTimestamp()), semantic);
            return [.. Fusion.Fuse([(snapshot, outcome)], 5).Select(h => Path.GetRelativePath(Root, h.Path).Replace('\\', '/'))];
        }
    }

    [Test]
    public async Task Hamming_dot_matches_the_float_dot_of_the_signs()
    {
        var rng = new Random(7);
        var a = new float[SignCodes.DIMENSIONS];
        var b = new float[SignCodes.DIMENSIONS];
        for (var d = 0; d < a.Length; d++)
        {
            a[d] = (float)(rng.NextDouble() - 0.5);
            b[d] = (float)(rng.NextDouble() - 0.5);
        }

        var codeA = new byte[SignCodes.BYTES];
        var codeB = new byte[SignCodes.BYTES];
        SignCodes.Encode(a, codeA);
        SignCodes.Encode(b, codeB);
        var expected = 0f;
        for (var d = 0; d < a.Length; d++)
        {
            expected += MathF.Sign(a[d] >= 0 ? 1 : -1) * MathF.Sign(b[d] >= 0 ? 1 : -1);
        }
        await Assert.That(SignCodes.Dot(codeA, codeB)).IsEqualTo(expected / SignCodes.DIMENSIONS).Within(1e-6f);
    }

    [Test]
    public async Task Query_table_score_matches_the_float_dot_with_the_unit_signs()
    {
        var rng = new Random(9);
        var query = new float[SignCodes.DIMENSIONS];
        var other = new float[SignCodes.DIMENSIONS];
        for (var d = 0; d < query.Length; d++)
        {
            query[d] = (float)(rng.NextDouble() - 0.5);
            other[d] = (float)(rng.NextDouble() - 0.5);
        }

        var code = new byte[SignCodes.BYTES];
        SignCodes.Encode(other, code);
        var expected = 0f;
        for (var d = 0; d < query.Length; d++)
        {
            expected += query[d] * (other[d] >= 0 ? 1 : -1) / MathF.Sqrt(SignCodes.DIMENSIONS);
        }
        await Assert.That(SignCodes.Score(SignCodes.QueryTable(query), code)).IsEqualTo(expected).Within(1e-4f);
    }

    [Test]
    public async Task Centroid_lengths_match_summing_the_unit_sign_vectors()
    {
        var encoder = new FakeEncoder();
        var (snapshot, vectors) = Build(encoder);
        using (snapshot)
        using (vectors)
        {
            var mismatches = 0;
            for (var i = snapshot.RootCount; i < snapshot.EntryCount; i++)
            {
                var centroid = new float[SignCodes.DIMENSIONS];
                var hop = 0;
                for (var at = i; at >= snapshot.RootCount; at = (int)snapshot.Parents[at], hop++)
                {
                    if (VectorBuilder.IsEmpty(vectors.Empty, snapshot.Names[at]))
                    {
                        continue;
                    }

                    var code = vectors.Code(snapshot.Names[at]);
                    for (var d = 0; d < centroid.Length; d++)
                    {
                        var sign = (code[d >> 3] & (1 << (d & 7))) != 0 ? 1 : -1;
                        centroid[d] += VectorBuilder.Weight(Math.Min(hop, 2)) * sign / MathF.Sqrt(SignCodes.DIMENSIONS);
                    }
                }

                if (MathF.Abs(MathF.Sqrt(FakeEncoder.Dot(centroid, centroid)) - vectors.Norms[i]) > 1e-3f)
                {
                    mismatches++;
                }
            }
            await Assert.That(mismatches).IsEqualTo(0);
        }
    }

    [Test]
    public async Task Hashes_and_versions_embed_as_nothing()
    {
        var encoder = new FakeEncoder();
        var (snapshot, vectors) = Build(encoder);
        using (snapshot)
        using (vectors)
        {
            var empty = Enumerable.Range(0, snapshot.SegmentCount)
                .Where(s => VectorBuilder.IsEmpty(vectors.Empty, (uint)s))
                .Select(s => System.Text.Encoding.UTF8.GetString(snapshot.Segment((uint)s)))
                .ToHashSet();
            await Assert.That(empty).Contains("635c1867224a");
            await Assert.That(empty).Contains("2.1.286");
            await Assert.That(empty).DoesNotContain("Spotify.exe");
        }
    }

    [Test]
    public async Task A_query_with_no_shared_words_finds_the_file_by_meaning() =>
        await Assert.That(Search("music streaming app").Take(2)).Contains("AppData/Roaming/Spotify/Spotify.exe");

    [Test]
    public async Task Lexical_and_semantic_fuse_for_the_terminal_settings() =>
        await Assert.That(Search("windows terminal config")[0]).IsEqualTo("AppData/Local/Packages/Microsoft.WindowsTerminal_8wekyb3d8bbwe/LocalState/settings.json");

    [Test]
    public async Task Vectors_for_another_snapshot_are_refused()
    {
        var encoder = new FakeEncoder();
        var (snapshot, vectors) = Build(encoder);
        using (snapshot)
        using (vectors)
        {
            var parts = VectorBuilder.Build(snapshot, encoder);
            var stale = VectorWriter.ToBytes(parts, snapshot.Checksum + 1, encoder.Checksum);
            var path = Path.Combine(Path.GetTempPath(), $"fetchle-stale-{Guid.NewGuid():N}.fvec");
            File.WriteAllBytes(path, stale);
            try
            {
                await Assert.That(SegmentVectors.Open(path, snapshot, encoder.Checksum, out var problem)).IsNull();
                await Assert.That(problem).Contains("another snapshot");
            }
            finally
            {
                File.Delete(path);
            }
        }
    }

    [Test]
    public async Task A_flipped_byte_is_refused()
    {
        var encoder = new FakeEncoder();
        var (snapshot, vectors) = Build(encoder);
        using (snapshot)
        using (vectors)
        {
            var image = VectorWriter.ToBytes(VectorBuilder.Build(snapshot, encoder), snapshot.Checksum, encoder.Checksum);
            image[^5] ^= 0xFF;
            var path = Path.Combine(Path.GetTempPath(), $"fetchle-corrupt-{Guid.NewGuid():N}.fvec");
            File.WriteAllBytes(path, image);
            try
            {
                await Assert.That(SegmentVectors.Open(path, snapshot, encoder.Checksum, out var problem)).IsNull();
                await Assert.That(problem).Contains("corrupt");
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
