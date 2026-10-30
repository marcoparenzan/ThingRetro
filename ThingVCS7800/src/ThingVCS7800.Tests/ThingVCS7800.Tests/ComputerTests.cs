using ThingVCS7800.A7800;
namespace ThingVCS7800.Tests;
public class ComputerTests
{
    static byte[] Rom()
    {
        var rom = new byte[16384];
        rom[0] = 0x4c; rom[1] = 0; rom[2] = 0xc0;
        rom[0x3ffc] = 0; rom[0x3ffd] = 0xc0;
        return rom;
    }
    [Fact] public void EmptyOrPausedHostDoesNotExecute()
    {
        var host = new DevelopmentComputer();
        Assert.Equal(0, host.Advance(TimeSpan.FromSeconds(1)));
        host.Insert(Rom()); host.Paused = true;
        Assert.Equal(0, host.Advance(TimeSpan.FromSeconds(1)));
        Assert.Equal(0, host.Board.Cycles);
    }
    [Fact] public void FailedInsertPreservesExistingCartridgeAndState()
    {
        var host = new DevelopmentComputer(); host.Insert(Rom());
        var cartridge = host.Board.CartridgeSocket.Chip;
        Assert.Throws<ArgumentException>(() => host.Insert(new byte[5]));
        Assert.Same(cartridge, host.Board.CartridgeSocket.Chip); Assert.False(host.Paused);
    }
    [Fact] public void PacingPreservesFractionAndBoundsCatchup()
    {
        var host = new DevelopmentComputer(); host.Insert(Rom());
        for (int i = 0; i < 100; i++) host.Advance(TimeSpan.FromMilliseconds(1));
        Assert.InRange(host.Board.Cycles, 178976, 178977);
        long before = host.Board.Cycles;
        Assert.InRange(host.Advance(TimeSpan.FromSeconds(10)), 89488, 89489);
        Assert.InRange(host.Board.Cycles - before, 89488, 89489);
    }
    [Fact] public void ResetAndEjectHaveExplicitLifecycle()
    {
        var host = new DevelopmentComputer(); host.Insert(Rom());
        host.Advance(TimeSpan.FromMilliseconds(1)); host.Reset();
        Assert.Equal(0, host.Board.Cycles); Assert.True(host.HasCartridge);
        host.Eject(); Assert.False(host.HasCartridge); Assert.True(host.Paused);
        Assert.Equal(0, host.Advance(TimeSpan.FromMilliseconds(1)));
    }
    [Fact] public void NegativeTimeIsRejected() => Assert.Throws<ArgumentOutOfRangeException>(() => new DevelopmentComputer().Advance(TimeSpan.FromSeconds(-1)));
}
