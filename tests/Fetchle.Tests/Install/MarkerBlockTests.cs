using Fetchle.Cli.Install;

namespace Fetchle.Tests.Install;

public class MarkerBlockTests
{
    const string BODY = "export PATH=\"/x/bin:$PATH\"";
    static readonly string Block = MarkerBlock.Render(BODY);

    [Test]
    public async Task Insert_into_an_empty_file_is_just_the_block()
    {
        var edit = MarkerBlock.Apply("", BODY);
        await Assert.That(edit.Change).IsEqualTo(BlockChange.Inserted);
        await Assert.That(edit.Content).IsEqualTo(Block + "\n");
    }

    [Test]
    public async Task Insert_leaves_existing_lines_alone_and_adds_a_blank_separator()
    {
        var edit = MarkerBlock.Apply("alias a=b\n", BODY);
        await Assert.That(edit.Content).IsEqualTo("alias a=b\n\n" + Block + "\n");
    }

    [Test]
    public async Task Insert_after_a_file_without_a_trailing_newline_still_separates()
    {
        var edit = MarkerBlock.Apply("alias a=b", BODY);
        await Assert.That(edit.Content).IsEqualTo("alias a=b\n\n" + Block + "\n");
    }

    [Test]
    public async Task Applying_twice_changes_nothing_the_second_time()
    {
        var first = MarkerBlock.Apply("alias a=b\n", BODY);
        var second = MarkerBlock.Apply(first.Content, BODY);
        await Assert.That(second.Change).IsEqualTo(BlockChange.Unchanged);
        await Assert.That(second.Content).IsEqualTo(first.Content);
    }

    [Test]
    public async Task A_different_body_replaces_the_block_in_place()
    {
        var original = "before\n\n" + MarkerBlock.Render("old") + "\nafter\n";
        var edit = MarkerBlock.Apply(original, BODY);
        await Assert.That(edit.Change).IsEqualTo(BlockChange.Replaced);
        await Assert.That(edit.Content).IsEqualTo("before\n\n" + Block + "\nafter\n");
    }

    [Test]
    public async Task A_hand_edited_block_counts_as_replaced()
    {
        var edited = $"{MarkerBlock.START}\n{BODY}\n# my own line\n{MarkerBlock.END}\n";
        var edit = MarkerBlock.Apply(edited, BODY);
        await Assert.That(edit.Change).IsEqualTo(BlockChange.Replaced);
        await Assert.That(edit.Content).IsEqualTo(Block + "\n");
    }

    [Test]
    public async Task Duplicate_blocks_collapse_to_one()
    {
        var doubled = Block + "\nmiddle\n" + Block + "\n";
        var edit = MarkerBlock.Apply(doubled, BODY);
        await Assert.That(edit.Change).IsEqualTo(BlockChange.Replaced);
        await Assert.That(MarkerBlock.Find(edit.Content).Count).IsEqualTo(1);
        await Assert.That(edit.Content).IsEqualTo(Block + "\nmiddle\n");
    }

    [Test]
    public async Task Remove_after_insert_restores_the_original()
    {
        var original = "alias a=b\nexport X=1\n";
        var inserted = MarkerBlock.Apply(original, BODY).Content;
        await Assert.That(MarkerBlock.Remove(inserted)).IsEqualTo(original);
    }

    [Test]
    public async Task Remove_after_insert_into_an_empty_file_leaves_nothing()
    {
        var inserted = MarkerBlock.Apply("", BODY).Content;
        await Assert.That(MarkerBlock.Remove(inserted)).IsEqualTo("");
    }

    [Test]
    public async Task Remove_without_a_block_changes_nothing()
    {
        var text = "alias a=b\n";
        await Assert.That(MarkerBlock.Remove(text)).IsEqualTo(text);
    }

    [Test]
    public async Task A_start_marker_without_an_end_is_not_a_block()
    {
        var dangling = $"keep\n{MarkerBlock.START}\nuser text\n";
        await Assert.That(MarkerBlock.Find(dangling)).IsEmpty();
        await Assert.That(MarkerBlock.Remove(dangling)).IsEqualTo(dangling);
    }

    [Test]
    public async Task Find_reads_blocks_in_a_crlf_file()
    {
        var crlf = $"{MarkerBlock.START}\r\n{BODY}\r\n{MarkerBlock.END}\r\n";
        await Assert.That(MarkerBlock.Find(crlf)).IsEquivalentTo([Block]);
    }
}
