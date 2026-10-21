namespace Fetchle.Cli.Install;

internal sealed class ShellRcBlock(string file) : IPathPersistence
{
    const string BACKUP_SUFFIX = ".bak";
    const string TEMP_SUFFIX = ".fetchle-tmp";

    public int Count(string entry)
    {
        var expected = MarkerBlock.Render(ShellProfile.PathBody(entry));
        return MarkerBlock.Find(Read()).Count(block => block == expected);
    }

    public PathAdd Add(string entry)
    {
        var edit = MarkerBlock.Apply(Read(), ShellProfile.PathBody(entry));
        if (edit.Change != BlockChange.Unchanged)
        {
            if (edit.Change == BlockChange.Replaced)
            {
                File.Copy(file, file + BACKUP_SUFFIX, overwrite: true);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            WriteAtomically(edit.Content);
        }

        return new PathAdd(new PathEdit(PathEditKinds.SHELL_RC_BLOCK, entry, file), edit.Change == BlockChange.Replaced);
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

        WriteAtomically(updated);
    }

    // someone else's rc file: never truncate it in place. write a sibling and rename it over, into the real file
    // when the rc is a symlink into a dotfiles repo, keeping its permissions
    void WriteAtomically(string content)
    {
        var target = File.Exists(file) ? new FileInfo(file).ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? file : file;
        var temp = target + TEMP_SUFFIX;
        File.WriteAllText(temp, content);
        if (!OperatingSystem.IsWindows() && File.Exists(target))
        {
            File.SetUnixFileMode(temp, File.GetUnixFileMode(target));
        }
        File.Move(temp, target, overwrite: true);
    }

    string Read() => File.Exists(file) ? File.ReadAllText(file) : "";
}
