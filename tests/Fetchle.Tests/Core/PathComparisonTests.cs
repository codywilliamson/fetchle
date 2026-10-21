using Fetchle.Core.Storage;
using Fetchle.Core.Walking;

namespace Fetchle.Tests.Core;

public class PathComparisonTests
{
    static readonly string ROOT = P("/Volumes/Data/Projects");

    // windows spells these with backslashes, and covers matches on the native separator
    static string P(string path) => path.Replace('/', Path.DirectorySeparatorChar);

    [Test]
    public async Task A_case_sensitive_comparison_does_not_cover_a_sibling_that_differs_by_case()
    {
        await Assert.That(RootsConfig.Covers(ROOT, P("/Volumes/Data/projects/app"), StringComparison.Ordinal)).IsFalse();
        await Assert.That(RootsConfig.Covers(ROOT, P("/Volumes/Data/projects"), StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task A_case_sensitive_comparison_still_covers_the_exact_spelling()
    {
        await Assert.That(RootsConfig.Covers(ROOT, ROOT, StringComparison.Ordinal)).IsTrue();
        await Assert.That(RootsConfig.Covers(ROOT, ROOT + P("/app"), StringComparison.Ordinal)).IsTrue();
        await Assert.That(RootsConfig.Covers(ROOT, ROOT + "-other", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task A_case_insensitive_comparison_folds_case()
    {
        await Assert.That(RootsConfig.Covers(ROOT, P("/volumes/data/PROJECTS/app"), StringComparison.OrdinalIgnoreCase)).IsTrue();
    }

    [Test]
    public async Task The_two_argument_covers_follows_the_platform_default_on_linux()
    {
        if (!OperatingSystem.IsLinux())
        {
            Skip.Test("only linux is case-sensitive without asking the volume");
        }

        await Assert.That(PathComparison.For(ROOT)).IsEqualTo(StringComparison.Ordinal);
        await Assert.That(RootsConfig.Covers(ROOT, P("/Volumes/Data/projects/app"))).IsFalse();
    }

    // a volume answers for the flipped spelling of a path that exists
    [Test]
    public async Task A_volume_that_resolves_a_case_flipped_path_is_case_insensitive()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ROOT };

        await Assert.That(PathComparison.ForVolume(ROOT, existing.Contains)).IsEqualTo(StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task A_volume_that_does_not_resolve_a_case_flipped_path_is_case_sensitive()
    {
        var existing = new HashSet<string>(StringComparer.Ordinal) { ROOT };

        await Assert.That(PathComparison.ForVolume(ROOT, existing.Contains)).IsEqualTo(StringComparison.Ordinal);
    }

    [Test]
    public async Task A_path_that_is_not_there_keeps_the_case_insensitive_default()
    {
        await Assert.That(PathComparison.ForVolume(ROOT, _ => false)).IsEqualTo(StringComparison.OrdinalIgnoreCase);
        await Assert.That(PathComparison.ForVolume("/123/456", _ => true)).IsEqualTo(StringComparison.OrdinalIgnoreCase);
    }
}
