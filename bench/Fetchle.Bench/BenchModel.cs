using Fetchle.Core.Embedding;

namespace Fetchle.Bench;

// the real embedding model for benches that rank the way the exe ships: FETCHLE_MODEL, else artifacts/model from
// `dotnet build.cs model`. opened once per process and never closed, like the exe's
static class BenchModel
{
    static readonly Lazy<StaticModel> Model = new(Open);

    public static StaticModel Instance => Model.Value;

    static StaticModel Open()
    {
        var fromEnv = Environment.GetEnvironmentVariable(StaticModel.ENV_PATH);
        var path = !string.IsNullOrEmpty(fromEnv)
            ? (Directory.Exists(fromEnv) ? Path.Combine(fromEnv, StaticModel.FILE_NAME) : fromEnv)
            : Path.Combine(RepoDir(), "artifacts", "model", StaticModel.FILE_NAME);
        return StaticModel.Open(path, verifyChecksum: false)
            ?? throw new FileNotFoundException($"no model at {path}; run `dotnet build.cs model` or set {StaticModel.ENV_PATH}");
    }

    static string RepoDir()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "fetchle.slnx")))
            {
                return dir.FullName;
            }
        }
        throw new InvalidOperationException("fetchle.slnx not found above the bench binaries");
    }
}
