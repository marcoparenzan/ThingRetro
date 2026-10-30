using ThingVCS7800.A7800;
namespace ThingVCS7800.Tests;
public class SlowClockTests
{
    static DevelopmentComputer Host(ushort address, bool read = false, bool loopAccess = false)
    {
        var rom = new byte[16384];
        byte[] program = [0xa9,0, (byte)(read ? 0xad : 0x8d),(byte)address,(byte)(address >> 8),0x4c,(byte)(loopAccess ? 2 : 5),0xc0];
        program.CopyTo(rom, 0); rom[0x3ffc] = 0; rom[0x3ffd] = 0xc0;
        var host = new DevelopmentComputer(); host.Insert(rom); return host;
    }
    [Theory][InlineData(1,false)][InlineData(12,true)][InlineData(0x280,false)][InlineData(0x284,true)]
    public void DecodedSlowAccessAddsTwoMasterClocks(int address, bool read)
    {
        var host = Host((ushort)address, read);
        for (int i = 0; i < 100; i++) host.Board.Tick();
        Assert.Equal(100, host.Board.Cycles); Assert.Equal(1, host.Board.SlowCpuAccesses);
        Assert.Equal(402, host.Board.MasterClocks);
    }
    [Theory][InlineData(0x1800)][InlineData(0x40)][InlineData(0x20)]
    public void FastAccessRetainsFourClockSlots(int address)
    {
        var host = Host((ushort)address);
        for (int i = 0; i < 100; i++) host.Board.Tick();
        Assert.Equal(0, host.Board.SlowCpuAccesses); Assert.Equal(400, host.Board.MasterClocks);
    }
    [Fact] public void InspectionAndMariaBusReadsDoNotChargeCpuSlowAccesses()
    {
        var host = new DevelopmentComputer(); host.Board.Read(12); host.Board.Write(1, 0);
        Assert.Equal(0, host.Board.MasterClocks); Assert.Equal(0, host.Board.SlowCpuAccesses);
    }
    [Fact] public void PacingUsesElapsedMasterClocksInsteadOfCpuSlotCount()
    {
        var fast = Host(0x1800, loopAccess: true); var slow = Host(1, loopAccess: true);
        for (int i = 0; i < 100; i++)
        {
            fast.Advance(TimeSpan.FromMilliseconds(1)); slow.Advance(TimeSpan.FromMilliseconds(1));
        }
        Assert.True(slow.Board.Cycles < fast.Board.Cycles);
        Assert.InRange(Math.Abs(slow.Board.MasterClocks - fast.Board.MasterClocks), 0, 6);
        Assert.True(slow.Board.SlowCpuAccesses > 0);
    }
    [Fact] public void ResetClearsTimingCounters()
    {
        var host = Host(1); host.Advance(TimeSpan.FromMilliseconds(1)); host.Reset();
        Assert.Equal(0, host.Board.MasterClocks); Assert.Equal(0, host.Board.SlowCpuAccesses);
    }
}
