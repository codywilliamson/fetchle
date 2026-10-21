using Fetchle.Core.Embedding;

namespace Fetchle.Cli.Commands;

// the embedding model is optional at runtime: without it fetchle ranks lexically and says so in doctor. search opens
// it with bounds checks only, rebuild and doctor verify its checksum (see ModelChecks.Validate)
internal static class ModelLoader
{
    // null when there's no model or it can't be read; a broken one is worth one line on stderr, a missing one isn't
    public static StaticModel? ForSearch(TextWriter error) => Open(error, verifyChecksum: false);

    public static StaticModel? ForRebuild(TextWriter error) => Open(error, verifyChecksum: true);

    static StaticModel? Open(TextWriter error, bool verifyChecksum)
    {
        try
        {
            return StaticModel.FindBesideExe(verifyChecksum);
        }
        catch (ModelUnreadableException e)
        {
            error.WriteLine($"fetchle: model unusable, ranking by name only: {e.Message}");
            return null;
        }
    }
}
