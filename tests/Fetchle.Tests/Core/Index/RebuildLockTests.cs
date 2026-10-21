using Fetchle.Core.Index;

namespace Fetchle.Tests.Core.Index;

// readers tell "a rebuild is running" from the writer lock, so the probe must see a held lock and never take one
public class RebuildLockTests
{
    [Test]
    public async Task No_lock_file_means_no_rebuild()
    {
        using var index = new TempIndex();

        await Assert.That(index.Store.IsRebuilding()).IsFalse();
    }

    [Test]
    public async Task A_held_write_lock_means_a_rebuild_is_running()
    {
        using var index = new TempIndex();
        using (new FileStream(index.Store.LockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, bufferSize: 1))
        {
            await Assert.That(index.Store.IsRebuilding()).IsTrue();
        }

        await Assert.That(index.Store.IsRebuilding()).IsFalse();
    }

    [Test]
    public async Task Probing_leaves_the_lock_free_for_a_writer()
    {
        using var index = new TempIndex();
        File.WriteAllText(index.Store.LockPath, "");

        index.Store.IsRebuilding();

        using var writer = new FileStream(index.Store.LockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None, bufferSize: 1);
        await Assert.That(writer.CanWrite).IsTrue();
    }
}
