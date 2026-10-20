using System.Text;
using Fetchle.Core.Index;

namespace Fetchle.Tests.Core.Index;

public class AsciiFinderTests
{
    // random haystacks heavy in the needle's letters, both cases, against a plain scalar reference
    [Test]
    public async Task Finds_what_a_scalar_search_finds()
    {
        var rng = new Random(11);
        const string ALPHABET = "sSeEtTiInNgG._-0aZ\0é";
        string[] needles = ["s", "settings", "SETTINGS", ".json", "_", "z", "gs", "e.t", "tt", "settingssettingssettingssettingsx"];
        for (var round = 0; round < 3000; round++)
        {
            var length = rng.Next(0, 300);
            var chars = new char[length];
            for (var i = 0; i < length; i++)
            {
                chars[i] = ALPHABET[rng.Next(ALPHABET.Length)];
            }

            var haystack = Encoding.UTF8.GetBytes(chars);
            foreach (var needle in needles)
            {
                var bytes = Encoding.ASCII.GetBytes(needle);
                await Assert.That(AsciiFinder.IndexOf(haystack, bytes)).IsEqualTo(Reference(haystack, bytes));
            }
        }
    }

    static int Reference(byte[] haystack, byte[] needle)
    {
        for (var i = 0; i + needle.Length <= haystack.Length; i++)
        {
            if (Ascii.EqualsIgnoreCase(haystack.AsSpan(i, needle.Length), needle))
            {
                return i;
            }
        }
        return -1;
    }
}
