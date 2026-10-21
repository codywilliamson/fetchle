using Fetchle.Core.Embedding;

namespace Fetchle.Tests.Core.Embedding;

// finds the real model the way a dev box and CI both have it: FETCHLE_MODEL, else artifacts/model from `dotnet build.cs model`
static class ModelFiles
{
    public const string MISSING = "no potion-retrieval-32M.fmodel, run `dotnet build.cs model` or set FETCHLE_MODEL";

    public static string? Find()
    {
        var fromEnv = Environment.GetEnvironmentVariable(StaticModel.ENV_PATH);
        if (!string.IsNullOrEmpty(fromEnv))
        {
            return Directory.Exists(fromEnv) ? Path.Combine(fromEnv, StaticModel.FILE_NAME) : fromEnv;
        }

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "fetchle.slnx")))
            {
                return Path.Combine(dir.FullName, "artifacts", "model", StaticModel.FILE_NAME);
            }
        }

        return null;
    }

    // the real model, or a skipped test
    public static StaticModel OpenOrSkip()
    {
        var path = Find();
        var model = path is null ? null : StaticModel.Open(path);
        if (model is null)
        {
            Skip.Test(MISSING);
        }

        return model!;
    }

    public static string RepoFile(string relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "fetchle.slnx")))
            {
                return Path.Combine(dir.FullName, relative);
            }
        }

        throw new InvalidOperationException("fetchle.slnx not found above the test binaries");
    }
}
