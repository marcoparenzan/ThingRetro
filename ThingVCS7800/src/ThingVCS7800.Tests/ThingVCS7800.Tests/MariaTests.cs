using ThingVCS;
using ThingVCS7800.A7800;
namespace ThingVCS7800.Tests;
public class MariaTests
{
    static void Advance(Maria chip, int clocks) { for (int i = 0; i < clocks; i++) chip.Tick(); }
    [Fact] public void RasterAndStatusTrackFrameBoundaries()
    {
        var chip = new Maria(); Assert.Equal(0xb5, chip.ReadRegister(8, 0x35));
        Advance(chip, 16 * 454); Assert.False(chip.Vblank); Assert.Equal(0x35, chip.ReadRegister(8, 0xb5));
        Advance(chip, 242 * 454); Assert.True(chip.Vblank);
        Advance(chip, 4 * 454); Assert.Equal(1, chip.Frames); Assert.Equal(0, chip.Scanline);
    }
    [Fact] public void BackgroundIsSampledAtPixelTime()
    {
        var chip = new Maria(); chip.WriteRegister(0, 0x3a);
        Advance(chip, 16 * 454 + 69); Assert.Equal(0x3a, chip.Framebuffer.Span[0]);
        chip.WriteRegister(0, 0x7b); chip.Tick();
        Assert.Equal(0x3a, chip.Framebuffer.Span[0]); Assert.Equal(0x7b, chip.Framebuffer.Span[1]);
    }
    [Fact] public void WsyncReleasesAtNextLineAndResetClearsRegisters()
    {
        var chip = new Maria(); Advance(chip, 100); chip.WriteRegister(4, 0);
        Assert.True(chip.WaitSync); Advance(chip, 353); Assert.True(chip.WaitSync);
        chip.Tick(); Assert.False(chip.WaitSync);
        chip.WriteRegister(12, 0x18); chip.WriteRegister(16, 0x40); chip.WriteRegister(28, 0x40);
        Assert.Equal(0x1840, chip.DisplayListPointer); Assert.Equal(0x40, chip.Control);
        chip.Reset(); Assert.Equal(0, chip.DisplayListPointer); Assert.Equal(0, chip.Control);
    }
    [Fact] public void CpuDemoDrivesActualBackdropRegisters()
    {
        var host = new DevelopmentComputer(); host.Insert(DevelopmentRoms.Backdrop(0x5a));
        for (int i = 0; i < 30000; i++) host.Board.Tick();
        var maria = Assert.IsType<Maria>(host.Board.MariaSocket.Chip);
        Assert.Equal(1, maria.Frames); Assert.All(maria.Framebuffer.ToArray(), pixel => Assert.Equal(0x5a, pixel));
    }
    [Fact] public void BoardConnectsWsyncToRdyAndRemovalReleasesCpu()
    {
        var host = new DevelopmentComputer(); host.Board.Write(0x24, 0);
        Assert.False(host.Board.CpuSocket.Chip!.RdyInput());
        host.Board.MariaSocket.Remove(); Assert.True(host.Board.CpuSocket.Chip.RdyInput());
    }
    [Fact] public void ApproximatePaletteHasOpaqueGrayscaleEndpoints()
    {
        Assert.Equal(unchecked((int)0xff000000), Maria.ApproximateArgb(0));
        Assert.Equal(unchecked((int)0xffffffff), Maria.ApproximateArgb(15));
        Assert.Equal(Maria.ApproximateArgb(10), Maria.ApproximateArgb(0x3a, true));
    }
}
