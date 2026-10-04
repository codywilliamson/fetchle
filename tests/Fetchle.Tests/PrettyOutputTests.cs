using Fetchle.Cli;
using Fetchle.Core;
using XenoAtom.Ansi;
using XenoAtom.Terminal;
using XenoAtom.Terminal.Backends;

namespace Fetchle.Tests;

[NotInParallel(nameof(Terminal))]
public class PrettyOutputTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
    static readonly string FilePath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "app", "Settings.json"));

    static string Render(TerminalCapabilities capabilities)
    {
        var backend = new InMemoryTerminalBackend(new TerminalSize(120, 40), capabilities);
        using var _ = Terminal.Open(backend, new TerminalOptions(), force: true);
        var result = new SearchResult([new SearchHit(FilePath, 1, 1_536, Now.AddDays(-2))], 1, TimeSpan.FromMilliseconds(3), null);
        PrettyOutput.Write(result, "settings", Now);
        return backend.GetOutText();
    }

    [Test]
    public async Task Links_highlights_and_dims()
    {
        var text = Render(new TerminalCapabilities { AnsiEnabled = true, ColorLevel = TerminalColorLevel.TrueColor, SupportsOsc8Links = true, IsOutputRedirected = false, IsInputRedirected = false });
        await Assert.That(text).Contains("\e]8;;" + new Uri(FilePath).AbsoluteUri);
        await Assert.That(text).Contains("Settings");
        await Assert.That(text).Contains("1.5 KB  2d ago");
        await Assert.That(text).Contains("1 matches, showing 1, 3ms");
        await Assert.That(text).Contains("\e[");
    }

    [Test]
    public async Task No_ansi_means_no_escapes()
    {
        var text = Render(new TerminalCapabilities { AnsiEnabled = false, ColorLevel = TerminalColorLevel.None, IsOutputRedirected = true, IsInputRedirected = true });
        await Assert.That(text).DoesNotContain("\e");
        await Assert.That(text).Contains(FilePath + "  1.5 KB  2d ago");
    }
}
