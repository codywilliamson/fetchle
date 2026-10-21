using Fetchle.Core.Embedding;

namespace Fetchle.Tests.Core.Embedding;

// encoding and the file contract on the hand-made model
public class StaticModelTests
{
    const float TOLERANCE = 0.01f;

    [Test]
    public async Task Encode_is_the_normalized_mean_of_the_rows()
    {
        using var model = TinyModel.Open();
        var vector = new float[4];

        var known = model.Encode("played", vector);

        await Assert.That(known).IsTrue();
        AssertClose(vector, TinyModel.Expected(TinyModel.PLAY, TinyModel.ED));
    }

    [Test]
    public async Task Unknown_tokens_are_ignored_not_averaged_in()
    {
        using var model = TinyModel.Open();
        var vector = new float[4];

        model.Encode("played zzzz cafe", vector);

        AssertClose(vector, TinyModel.Expected(TinyModel.PLAY, TinyModel.ED, TinyModel.CAFE));
        await Assert.That(Math.Abs(vector.Sum(v => v * v) - 1)).IsLessThan(1e-3f);
    }

    [Test]
    public async Task Text_with_no_known_tokens_gives_false_and_zeros()
    {
        using var model = TinyModel.Open();
        var vector = new float[4] { 1, 1, 1, 1 };

        await Assert.That(model.Encode("zzzz qqqq", vector)).IsFalse();
        await Assert.That(vector.All(v => v == 0)).IsTrue();
        await Assert.That(model.Encode("", vector)).IsFalse();
    }

    [Test]
    public async Task The_wrong_vector_length_is_a_caller_bug()
    {
        using var model = TinyModel.Open();

        await Assert.That(() => model.Encode("cafe", new float[3])).Throws<ArgumentException>();
    }

    [Test]
    public async Task Wide_rows_take_the_vector_path_and_still_match_f32()
    {
        const int DIMS = 100;
        var random = new Random(7);
        var weights = Enumerable.Range(0, 3 * DIMS).Select(_ => (float)(random.NextDouble() * 2 - 1)).ToArray();
        using var model = StaticModel.FromBytes(ModelWriter.Build(["[UNK]", "a", "b"], weights, DIMS, 0, "##"));
        var vector = new float[DIMS];

        model.Encode("a b", vector);

        var expected = new float[DIMS];
        for (var d = 0; d < DIMS; d++)
        {
            expected[d] = weights[DIMS + d] / Norm(weights.AsSpan(DIMS, DIMS)) + weights[2 * DIMS + d] / Norm(weights.AsSpan(2 * DIMS, DIMS));
        }

        await Assert.That(Cosine(vector, expected)).IsGreaterThan(0.999f);
    }

    [Test]
    public async Task Mean_is_the_average_of_the_normalized_rows()
    {
        using var model = TinyModel.Open();
        var expected = new float[4];
        foreach (var id in Enumerable.Range(0, 12))
        {
            var row = id == TinyModel.PAD ? new float[4] : TinyModel.Expected(id);
            for (var d = 0; d < 4; d++)
            {
                expected[d] += row[d] / 12;
            }
        }

        await Assert.That(model.Dimensions).IsEqualTo(4);
        AssertClose(model.Mean.ToArray(), expected);
    }

    [Test]
    public async Task Checksum_is_the_header_hash_and_changes_with_the_content()
    {
        var image = TinyModel.Image();
        using var a = StaticModel.FromBytes(image);
        using var same = StaticModel.FromBytes((byte[])image.Clone());
        var other = (byte[])image.Clone();
        other[^9] ^= 1;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(other.AsSpan(ModelFormat.CHECKSUM_OFFSET), ModelChecks.Checksum(other));
        using var b = StaticModel.FromBytes(other);

        await Assert.That(a.Checksum).IsEqualTo(same.Checksum);
        await Assert.That(a.Checksum).IsNotEqualTo(b.Checksum);
    }

    [Test]
    public async Task Open_maps_a_file_and_a_missing_file_is_null()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, StaticModel.FILE_NAME);
        File.WriteAllBytes(path, TinyModel.Image());

        using var model = StaticModel.Open(path);
        var vector = new float[4];

        await Assert.That(model).IsNotNull();
        await Assert.That(model!.Encode("cafe", vector)).IsTrue();
        await Assert.That(StaticModel.Open(Path.Combine(dir.Path, "nope.fmodel"))).IsNull();
    }

    [Test]
    public async Task A_mapped_model_can_be_deleted_while_open()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, StaticModel.FILE_NAME);
        File.WriteAllBytes(path, TinyModel.Image());
        using var model = StaticModel.Open(path)!;

        File.Delete(path);
        var vector = new float[4];

        await Assert.That(model.Encode("cafe", vector)).IsTrue();
    }

    [Test]
    public async Task Every_truncation_is_unreadable()
    {
        var image = TinyModel.Image();
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "m.fmodel");

        for (var length = 1; length < image.Length; length++)
        {
            File.WriteAllBytes(path, image[..length]);
            await Assert.That(() => StaticModel.Open(path)).Throws<ModelUnreadableException>();
        }
    }

    [Test]
    public async Task Every_corrupted_byte_is_unreadable()
    {
        var image = TinyModel.Image();
        foreach (var flip in (byte[])[0x01, 0x80, 0xFF])
        {
            for (var offset = 0; offset < image.Length; offset++)
            {
                var damaged = (byte[])image.Clone();
                damaged[offset] ^= flip;
                await Assert.That(() => StaticModel.FromBytes(damaged)).Throws<ModelUnreadableException>();
            }
        }
    }

    // past a matching checksum the structure checks are the last line: any byte value either fails them or
    // leaves a model every encode can run on without throwing
    [Test]
    public async Task Structure_checks_hold_without_the_checksum()
    {
        var image = TinyModel.Image();
        var rejected = 0;
        var vector = new float[4];
        for (var offset = ModelFormat.CHECKSUMMED_FROM; offset < image.Length; offset++)
        {
            foreach (var value in (byte[])[0x00, 0x01, 0x7F, 0x80, 0xFF])
            {
                var damaged = (byte[])image.Clone();
                damaged[offset] = value;
                using var model = StaticModel.FromBytesSkippingChecksum(damaged);
                if (model is null)
                {
                    rejected++;
                    continue;
                }

                model.Encode("Played café.日本 playx ééé", vector);
            }
        }

        await Assert.That(rejected).IsGreaterThan(0);
    }

    [Test]
    public async Task The_env_var_wins_over_the_exe_dir_and_a_bad_path_does_not_fall_back()
    {
        using var exeDir = new TempDirectory();
        using var envDir = new TempDirectory();
        File.WriteAllBytes(Path.Combine(exeDir.Path, StaticModel.FILE_NAME), TinyModel.Image());
        File.WriteAllBytes(Path.Combine(envDir.Path, StaticModel.FILE_NAME), TinyModel.Image(maxWordChars: 8));

        using var beside = StaticModel.Find(null, exeDir.Path);
        using var byFile = StaticModel.Find(Path.Combine(envDir.Path, StaticModel.FILE_NAME), exeDir.Path);
        using var byDir = StaticModel.Find(envDir.Path, exeDir.Path);

        await Assert.That(beside!.MaxWordChars).IsEqualTo(ModelWriter.DEFAULT_MAX_WORD_CHARS);
        await Assert.That(byFile!.MaxWordChars).IsEqualTo(8);
        await Assert.That(byDir!.MaxWordChars).IsEqualTo(8);
        await Assert.That(StaticModel.Find(Path.Combine(envDir.Path, "missing.fmodel"), exeDir.Path)).IsNull();
        using var emptyEnv = StaticModel.Find("", exeDir.Path);
        await Assert.That(emptyEnv).IsNotNull();
    }

    static void AssertClose(float[] actual, float[] expected)
    {
        for (var d = 0; d < expected.Length; d++)
        {
            if (Math.Abs(actual[d] - expected[d]) > TOLERANCE)
            {
                throw new InvalidOperationException($"dim {d}: {actual[d]} vs {expected[d]}");
            }
        }
    }

    static float Norm(ReadOnlySpan<float> v)
    {
        var sum = 0f;
        foreach (var x in v)
        {
            sum += x * x;
        }

        return MathF.Sqrt(sum);
    }

    static float Cosine(float[] a, float[] b)
    {
        float dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }

        return dot / MathF.Sqrt(na * nb);
    }

    sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("fetchle-model-").FullName;

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
