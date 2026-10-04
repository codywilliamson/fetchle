using Fetchle.Core.Walking;

namespace Fetchle.Tests.Core;

public class PruneRulesTests
{
    [Test]
    [Arguments("node_modules")]
    [Arguments(".git")]
    [Arguments("__pycache__")]
    [Arguments(".venv")]
    [Arguments("$RECYCLE.BIN")]
    public async Task Prunes_default_names(string name) =>
        await Assert.That(PruneRules.Default.ShouldPrune("/any/where", name)).IsTrue();

    [Test]
    public async Task Prunes_full_paths_by_parent_and_name()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "fetchle-prune"));
        var rules = new PruneRules([], [Path.Combine(root, "cache")]);
        await Assert.That(rules.ShouldPrune(root, "cache")).IsTrue();
        await Assert.That(rules.ShouldPrune(root + Path.DirectorySeparatorChar, "cache")).IsTrue();
        await Assert.That(rules.ShouldPrune(Path.Combine(root, "other"), "cache")).IsFalse();
        await Assert.That(rules.ShouldPrune(root, "src")).IsFalse();
    }

    [Test]
    [Arguments("src")]
    [Arguments("bin")]
    [Arguments("git")]
    public async Task Keeps_ordinary_names(string name) =>
        await Assert.That(PruneRules.Default.ShouldPrune("/any/where", name)).IsFalse();

    [Test]
    public async Task Name_case_follows_the_os()
    {
        var expected = !OperatingSystem.IsLinux();
        await Assert.That(PruneRules.Default.ShouldPrune("/any/where", "NODE_MODULES")).IsEqualTo(expected);
    }
}
