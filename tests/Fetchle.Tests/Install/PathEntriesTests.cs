using Fetchle.Cli.Install;

namespace Fetchle.Tests.Install;

public class PathEntriesTests
{
    const string ENTRY = @"C:\Users\me\AppData\Local\fetchle\bin";

    [Test]
    public async Task Add_to_an_empty_path_is_just_the_entry() =>
        await Assert.That(PathEntries.Add(null, ENTRY, PathRules.Windows)).IsEqualTo(ENTRY);

    [Test]
    public async Task Add_puts_the_entry_first() =>
        await Assert.That(PathEntries.Add(@"C:\a;C:\b", ENTRY, PathRules.Windows)).IsEqualTo($@"{ENTRY};C:\a;C:\b");

    [Test]
    public async Task Add_when_present_changes_nothing()
    {
        var path = $@"C:\a;{ENTRY};C:\b";
        await Assert.That(PathEntries.Add(path, ENTRY, PathRules.Windows)).IsEqualTo(path);
    }

    [Test]
    public async Task Add_collapses_duplicates_to_the_first_copy() =>
        await Assert.That(PathEntries.Add($@"C:\a;{ENTRY};C:\b;{ENTRY}", ENTRY, PathRules.Windows)).IsEqualTo($@"C:\a;{ENTRY};C:\b");

    [Test]
    public async Task Windows_matching_ignores_case_and_a_trailing_slash() =>
        await Assert.That(PathEntries.Count(@"C:\A;c:\users\ME\appdata\local\fetchle\bin\", ENTRY, PathRules.Windows)).IsEqualTo(1);

    [Test]
    public async Task Unix_matching_is_case_sensitive() =>
        await Assert.That(PathEntries.Count("/Opt/Bin:/opt/bin", "/opt/bin", PathRules.Unix)).IsEqualTo(1);

    [Test]
    public async Task Windows_matching_expands_env_vars()
    {
        Environment.SetEnvironmentVariable("FETCHLE_TEST_BASE", @"C:\base");
        await Assert.That(PathEntries.Count(@"%FETCHLE_TEST_BASE%\bin;C:\x", @"C:\base\bin", PathRules.Windows)).IsEqualTo(1);
    }

    [Test]
    public async Task Count_of_a_missing_entry_is_zero()
    {
        await Assert.That(PathEntries.Count(null, ENTRY, PathRules.Windows)).IsEqualTo(0);
        await Assert.That(PathEntries.Count(@"C:\a", ENTRY, PathRules.Windows)).IsEqualTo(0);
    }

    [Test]
    public async Task Remove_drops_every_copy_and_keeps_the_rest() =>
        await Assert.That(PathEntries.Remove($@"{ENTRY};C:\a;{ENTRY};C:\b", ENTRY, PathRules.Windows)).IsEqualTo(@"C:\a;C:\b");

    [Test]
    public async Task Remove_of_the_only_entry_leaves_an_empty_string() =>
        await Assert.That(PathEntries.Remove(ENTRY, ENTRY, PathRules.Windows)).IsEqualTo("");

    [Test]
    public async Task Add_then_remove_returns_the_original()
    {
        var original = "/usr/bin:/bin";
        var added = PathEntries.Add(original, "/home/me/.local/share/fetchle/bin", PathRules.Unix);
        await Assert.That(PathEntries.Remove(added, "/home/me/.local/share/fetchle/bin", PathRules.Unix)).IsEqualTo(original);
    }
}
