using Fetchle.Core.Storage;

namespace Fetchle.Tests.Core;

public class RootsConfigTests
{
    [Test]
    [Arguments("null")]
    [Arguments("  null\n")]
    public async Task A_top_level_null_config_is_unreadable_not_empty(string json)
    {
        var dir = Directory.CreateTempSubdirectory("fetchle-roots-");
        try
        {
            var path = Path.Combine(dir.FullName, "config.json");
            File.WriteAllText(path, json);

            await Assert.That(() => RootsConfig.Load(path)).Throws<InvalidDataException>();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Removing_one_spelling_keeps_a_root_that_differs_only_by_case()
    {
        var dir = Directory.CreateTempSubdirectory("fetchle-roots-");
        try
        {
            var upper = Path.Combine(dir.FullName, "Foo");
            var lower = Path.Combine(dir.FullName, "foo");
            List<string> roots = [upper, lower];

            await Assert.That(RootsConfig.Remove(roots, lower)).IsEqualTo(1);
            await Assert.That(roots).IsEquivalentTo(new[] { upper });
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Without_an_exact_spelling_removal_compares_the_way_the_volume_does()
    {
        var dir = Directory.CreateTempSubdirectory("fetchle-roots-");
        try
        {
            var root = Path.Combine(dir.FullName, "Foo");
            Directory.CreateDirectory(root);
            List<string> roots = [root];
            var otherCase = Path.Combine(dir.FullName, "FOO");
            var sameOnThisVolume = Fetchle.Core.Walking.PathComparison.For(root) == StringComparison.OrdinalIgnoreCase;

            await Assert.That(RootsConfig.Remove(roots, otherCase)).IsEqualTo(sameOnThisVolume ? 1 : 0);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
