using System.Text;
using ThingRetroEmu.Chips;

namespace ThingCommodore.Tests;

// The 1581 board on its own: the original DOS boots and talks to its chips
[TestClass]
public sealed class Board1581Tests
{
    static byte[] LoadRom()
    {
        var file = Path.Combine(AppContext.BaseDirectory, "roms", "c-drive-1581.rom");
        if (!File.Exists(file)) Assert.Inconclusive($"ROM missing: {file}");
        return File.ReadAllBytes(file);
    }

    [TestMethod]
    public void Rom_WithTheWrongSize_ShouldBeRefused()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new Board1581(new byte[16384]));
    }

}
