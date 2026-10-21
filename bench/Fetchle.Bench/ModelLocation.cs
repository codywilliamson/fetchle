using Fetchle.Core.Embedding;

namespace Fetchle.Bench;

// FETCHLE_MODEL first, then artifacts/model in the checkout the bench runs from
static class ModelLocation
{
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
}
