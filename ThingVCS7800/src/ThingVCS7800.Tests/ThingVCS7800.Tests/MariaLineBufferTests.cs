using ThingRetroEmu.Chips;
using ThingVCS;
namespace ThingVCS7800.Tests;

public class MariaLineBufferTests
{
    sealed class Memory : IBus
    {
        public byte[] Data { get; } = new byte[65536];
        public byte Read(ushort address) => Data[address];
        public void Write(ushort address, byte value) => Data[address] = value;
    }
    static (Maria chip, Memory memory) Setup()
    {
        var memory = new Memory();
        var chip = new Maria { Bus = memory };
        chip.WriteRegister(0, 6); chip.WriteRegister(1, 42);
        chip.WriteRegister(12, 0x18); chip.WriteRegister(28, 0x40);
        // Two-line zone: page 1B then 1A, one byte at horizontal position zero.
        memory.Data[0x1800] = 1; memory.Data[0x1801] = 0x19;
        byte[] list = [0, 31, 0x1a, 0, 0, 0];
        list.CopyTo(memory.Data, 0x1900);
        memory.Data[0x1b00] = 0x55;
        return (chip, memory);
    }
    static void Advance(Maria chip, int clocks)
    {
        for (int i = 0; i < clocks; i++) chip.Tick();
    }
    [Fact] public void DiagnosticTraceDoesNotPerformAdditionalBusReadsOrChangePixels()
    {
        var (plain, _) = Setup(); var (traced, _) = Setup();
        List<MariaDmaTrace> lines = [];
        traced.DmaTrace = lines.Add;
        Advance(plain, 18 * 454 + 90); Advance(traced, 18 * 454 + 90);
        Assert.Equal(plain.GraphicsReads, traced.GraphicsReads);
        Assert.Equal(plain.Framebuffer.ToArray(), traced.Framebuffer.ToArray());
        Assert.Equal(3, lines.Count);
        Assert.Equal((ushort)0x1900, lines[0].DisplayList);
        Assert.Equal(1, lines[0].ZoneOffset);
        Assert.Equal((ushort)0x1a00, Assert.Single(lines[0].Objects).Graphics);
        Assert.True(lines[0].Terminated); Assert.False(lines[0].Exhausted);
    }
    [Fact] public void FirstLineIsEmptyThenPreviousDmaIsDisplayed()
    {
        var (chip, _) = Setup();
        Advance(chip, 16 * 454 + 90);
        Assert.Equal(6, chip.Framebuffer.Span[0]);
        Advance(chip, 454);
        Assert.Equal(42, chip.Framebuffer.Span[320]);
        Advance(chip, 454);
        Assert.Equal(42, chip.Framebuffer.Span[640]); // Bootstrap holds the initial offset.
        Advance(chip, 454);
        Assert.Equal(6, chip.Framebuffer.Span[960]);
    }
    [Fact] public void ModifyingGraphicsAfterFetchDoesNotChangeBufferedLine()
    {
        var (chip, memory) = Setup();
        Advance(chip, 16 * 454 + 90);
        memory.Data[0x1b00] = 0;
        chip.WriteRegister(1, 99);
        Advance(chip, 454 + 89);
        Assert.Equal(99, chip.Framebuffer.Span[320]);
    }
    [Fact] public void ResetClearsBothLineBuffers()
    {
        var (chip, _) = Setup();
        Advance(chip, 17 * 454 + 90);
        Assert.Equal(42, chip.Framebuffer.Span[320]);
        chip.Reset();
        chip.WriteRegister(0, 7);
        Advance(chip, 18 * 454 + 90);
        Assert.Equal(7, chip.Framebuffer.Span[0]);
        Assert.Equal(7, chip.Framebuffer.Span[320]);
        Assert.Equal(7, chip.Framebuffer.Span[640]);
    }
    [Fact] public void VerticalBlankDoesNotLeakPreviousFrameLineRam()
    {
        var (chip, _) = Setup();
        Advance(chip, 17 * 454 + 90);
        chip.WriteRegister(28, 0);
        Advance(chip, 262 * 454);
        Assert.Equal(6, chip.Framebuffer.Span[0]);
        Assert.Equal(6, chip.Framebuffer.Span[320]);
    }
}
