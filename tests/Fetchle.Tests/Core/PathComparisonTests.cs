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

    [Test]
    public async Task A_volume_that_says_case_sensitive_compares_ordinally() =>
        await Assert.That(PathComparison.ForVolume(ROOT, _ => 1)).IsEqualTo(StringComparison.Ordinal);

    [Test]
    public async Task A_volume_that_folds_case_compares_ignoring_case() =>
        await Assert.That(PathComparison.ForVolume(ROOT, _ => 0)).IsEqualTo(StringComparison.OrdinalIgnoreCase);

    [Test]
    public async Task A_volume_that_wont_say_keeps_the_case_insensitive_default() =>
        await Assert.That(PathComparison.ForVolume(ROOT, _ => -1)).IsEqualTo(StringComparison.OrdinalIgnoreCase);

    [Test]
    public async Task The_real_volume_answers_on_macos()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Skip.Test("pathconf(_PC_CASE_SENSITIVE) is a macos question");
        }

        // a stock macos runner is apfs, case-insensitive; the call must return an answer, not an error
        await Assert.That(PathComparison.ForVolume(Path.GetTempPath(), Fetchle.Core.Walking.MacVolume.CaseSensitive)).IsEqualTo(StringComparison.OrdinalIgnoreCase);
    }
}
