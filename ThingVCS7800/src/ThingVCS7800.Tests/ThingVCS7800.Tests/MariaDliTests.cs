using ThingVCS;
using ThingRetroEmu.Chips;
using ThingVCS7800.A7800;
namespace ThingVCS7800.Tests;
public class MariaDliTests
{
    sealed class Memory : IBus
    {
        public byte[] Data = new byte[65536];
        public byte Read(ushort address) => Data[address];
        public void Write(ushort address, byte value) => Data[address] = value;
    }
    static (Maria, Memory) Setup(byte zone = 0x80)
    {
        var memory = new Memory(); var chip = new Maria { Bus = memory };
        chip.WriteRegister(12, 0x18); chip.WriteRegister(16, 0); chip.WriteRegister(28, 0x40);
        memory.Data[0x1800] = zone; memory.Data[0x1801] = 0x19;
        return (chip, memory);
    }
    static void Advance(Maria chip, int count) { for (int i = 0; i < count; i++) chip.Tick(); }
    [Fact] public void InitialDliPulsesAfterRenderingAndReleasesWithoutAcknowledgement()
    {
        var (chip, _) = Setup(); Advance(chip, 16 * 454 + 34); Assert.False(chip.Nmi);
        chip.Tick(); Assert.False(chip.Nmi);
        while (!chip.Nmi && chip.Scanline == 16) chip.Tick();
        Assert.True(chip.Nmi); Assert.Equal(1, chip.DisplayListInterrupts);
        Assert.True(chip.DmaBusy);
        chip.ReadRegister(8, 0); Assert.True(chip.Nmi);
        Advance(chip, 7); Assert.True(chip.Nmi); chip.Tick(); Assert.False(chip.Nmi);
        Assert.Equal(0, chip.UnsupportedLines);
    }
    [Fact] public void MultiLineZoneGeneratesOneEdgeThenNextZoneGeneratesAnother()
    {
        var (chip, memory) = Setup(0x81); memory.Data[0x1803] = 0x80; memory.Data[0x1804] = 0x19;
        Advance(chip, 17 * 454 + 35); Assert.Equal(1, chip.DisplayListInterrupts);
        while (chip.DisplayListInterrupts < 2 && chip.Scanline < 19) chip.Tick();
        Assert.Equal(18, chip.Scanline); Assert.Equal(2, chip.DisplayListInterrupts); Assert.True(chip.Nmi);
        Assert.True(chip.DmaBusy); // The next zone's DLI arrives during post-render DMA.
    }
    [Fact] public void DisabledDmaDoesNotGenerateDli()
    {
        var (chip, _) = Setup(); chip.WriteRegister(28, 0x60);
        Advance(chip, 17 * 454); Assert.Equal(0, chip.DisplayListInterrupts); Assert.False(chip.Nmi);
    }
    [Fact] public void ResetClearsPulseAndCounter()
    {
        var (chip, _) = Setup(); Advance(chip, 16 * 454 + 1); chip.Reset();
        Assert.False(chip.Nmi); Assert.Equal(0, chip.DisplayListInterrupts);
    }
    [Fact] public void CpuServicesDliInFlatMemoryHarness()
    {
        var (chip, memory) = Setup();
        byte[] main = [0x4c,0x00,0xc0]; main.CopyTo(memory.Data, 0xc000);
        byte[] handler = [0xee,0x00,0x20,0x40]; handler.CopyTo(memory.Data, 0xc600);
        memory.Data[0xfffc] = 0; memory.Data[0xfffd] = 0xc0;
        memory.Data[0xfffa] = 0; memory.Data[0xfffb] = 0xc6;
        var cpu = new ThingMOS.MOS6502 { Bus = memory, NmiInput = () => chip.Nmi }; cpu.Reset();
        for (int cycle = 0; cycle < 30000; cycle++)
        {
            cpu.Tick(); for (int i = 0; i < 4; i++) chip.Tick();
        }
        Assert.Equal(1, memory.Data[0x2000]); Assert.Equal(1, chip.DisplayListInterrupts);
    }
    [Fact] public void BoardWiresNmiAndDisconnectsRemovedCpu()
    {
        var host = new DevelopmentComputer(); host.Insert(DevelopmentRoms.DisplayListInterrupts());
        for (int i = 0; i < 10000 && !host.Board.MariaSocket.Chip!.Nmi; i++) host.Board.Tick();
        Assert.True(host.Board.MariaSocket.Chip!.Nmi);
        var cpu = host.Board.CpuSocket.Chip!; Assert.True(cpu.NmiInput());
        host.Board.CpuSocket.Remove(); Assert.False(cpu.NmiInput());
    }
}
