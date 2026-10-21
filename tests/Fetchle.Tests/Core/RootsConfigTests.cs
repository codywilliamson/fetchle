using Fetchle.Core.Storage;

namespace Fetchle.Tests.Core;

public class RootsConfigTests
{
    [Test]
    [Arguments("null")]
    [Arguments("  null\n")]
    public async Task A_top_level_null_config_is_unreadable_not_empty(string json)
    {
        var dir = Directory.CreateTempSubdirectory("fetchle-roots-");
        try
        {
            var path = Path.Combine(dir.FullName, "config.json");
            File.WriteAllText(path, json);

            await Assert.That(() => RootsConfig.Load(path)).Throws<InvalidDataException>();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
