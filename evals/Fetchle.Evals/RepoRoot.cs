namespace Fetchle.Evals;

public static class RepoRoot
{
    public static string Find()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "fetchle.slnx")))
            {
                return dir.FullName;
            }
        }
        throw new InvalidOperationException("can't find fetchle.slnx above " + AppContext.BaseDirectory);
    }
}
