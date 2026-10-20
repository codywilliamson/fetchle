namespace Fetchle.Cli.Install;

internal sealed class ShellRcBlock(string file) : IPathPersistence
{
    const string BACKUP_SUFFIX = ".bak";

    public int Count(string entry)
    {
        var expected = MarkerBlock.Render(ShellProfile.PathBody(entry));
        return MarkerBlock.Find(Read()).Count(block => block == expected);
    }

    public PathEdit Add(string entry)
    {
        var edit = MarkerBlock.Apply(Read(), ShellProfile.PathBody(entry));
        if (edit.Change != BlockChange.Unchanged)
        {
            if (edit.Change == BlockChange.Replaced)
            {
                File.Copy(file, file + BACKUP_SUFFIX, overwrite: true);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, edit.Content);
        }

        return new PathEdit(PathEditKinds.SHELL_RC_BLOCK, entry, file);
    }

    public void Remove(string entry)
    {
        if (!File.Exists(file))
        {
            return;
        }

        var original = Read();
        var updated = MarkerBlock.Remove(original);
        if (updated == original)
        {
            return;
        }

        // a file that only held our block was ours to make
        if (string.IsNullOrWhiteSpace(updated))
        {
            File.Delete(file);
            return;
        }

        File.WriteAllText(file, updated);
    }

    string Read() => File.Exists(file) ? File.ReadAllText(file) : "";
}
