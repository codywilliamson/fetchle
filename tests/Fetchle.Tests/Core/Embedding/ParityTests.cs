using System.Runtime.InteropServices;
using System.Text.Json;
using Fetchle.Core.Embedding;

namespace Fetchle.Tests.Core.Embedding;

// holds the C# encoder to Python model2vec on parity.json (regenerate: tools/model/parity.py). token ids must
// match exactly; vectors may differ by the int8 quantization, which the spike measured at mean cosine 0.9995
public class ParityTests
{
    const float MIN_COSINE = 0.99f;

    [Test]
    public async Task Token_ids_match_model2vec_exactly_and_vectors_stay_close()
    {
        using var model = ModelFiles.OpenOrSkip();
        using var fixture = JsonDocument.Parse(File.ReadAllBytes(ModelFiles.RepoFile("tests/Fetchle.Tests/Core/Embedding/parity.json")));
        var cases = fixture.RootElement.GetProperty("cases");
        var vector = new float[model.Dimensions];
        var failures = new List<string>();
        var cosines = new List<float>();
        var zeroCases = 0;

        foreach (var entry in cases.EnumerateArray())
        {
            var text = entry.GetProperty("text").GetString()!;
            var expectedIds = entry.GetProperty("ids").EnumerateArray().Select(i => i.GetInt32()).ToArray();
            var actualIds = model.TokenIds(text);
            if (!actualIds.SequenceEqual(expectedIds))
            {
                failures.Add($"ids for '{text}': [{string.Join(", ", actualIds)}] vs [{string.Join(", ", expectedIds)}]");
                continue;
            }

            var known = model.Encode(text, vector);
            if (entry.GetProperty("vector").ValueKind == JsonValueKind.Null)
            {
                zeroCases++;
                if (known || vector.Any(v => v != 0))
                {
                    failures.Add($"'{text}' has no known tokens but encoded to a vector");
                }

                continue;
            }

            var cosine = Cosine(vector, Decode(entry.GetProperty("vector").GetString()!));
            cosines.Add(cosine);
            if (!known || cosine < MIN_COSINE)
            {
                failures.Add($"cosine {cosine:F4} for '{text}'");
            }
        }

        Console.WriteLine($"parity: {cosines.Count} vectors, cosine min {cosines.Min():F5} mean {cosines.Average():F5}, {zeroCases} no-token cases, {cases.GetArrayLength()} cases");
        await Assert.That(failures).IsEmpty();
        await Assert.That(zeroCases).IsGreaterThan(0);
    }

    [Test]
    public async Task Fixture_belongs_to_the_pinned_revision()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllBytes(ModelFiles.RepoFile("tests/Fetchle.Tests/Core/Embedding/parity.json")));
        var source = File.ReadAllText(ModelFiles.RepoFile("tools/model/source.txt"));

        await Assert.That(source).Contains($"revision {fixture.RootElement.GetProperty("revision").GetString()}");
    }

    static float[] Decode(string base64)
    {
        var halves = MemoryMarshal.Cast<byte, Half>(Convert.FromBase64String(base64));
        var vector = new float[halves.Length];
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)halves[i];
        }

        return vector;
    }

    static float Cosine(float[] a, float[] b)
    {
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += (double)a[i] * b[i];
            na += (double)a[i] * a[i];
            nb += (double)b[i] * b[i];
        }

        return (float)(dot / Math.Sqrt(na * nb));
    }
}
