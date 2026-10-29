using ThingNES;
using ThingMOS;
using ThingRetroEmu.Chips;

namespace ThingNES.Tests;

public class HardwareTests
{
    static byte[] Rom(byte flags = 0, byte chr = 0)
    {
        var data = new byte[16 + 16384 + chr * 8192];
        "NES\u001a"u8.CopyTo(data);
        data[4] = 1; data[5] = chr; data[6] = flags;
        data[16 + 0x3ffc] = 0; data[16 + 0x3ffd] = 0x80;
        return data;
    }
    [Fact] public void NromMirrorsPrgAndRam()
    {
        var rom = Rom(); rom[16] = 0xab;
        var m = new NesMachine(Cartridge.Load(rom));
        Assert.Equal(0xab, m.Read(0xc000));
        m.Write(0x12, 0x43); Assert.Equal(0x43, m.Read(0x1812));
        m.Write(0x8000, 0); Assert.Equal(0xab, m.Read(0x8000));
    }
    [Fact] public void InvalidImagesAreRejected()
    {
        Assert.Throws<InvalidDataException>(() => Cartridge.Load(new byte[16]));
        Assert.Throws<InvalidDataException>(() => Cartridge.Load(Rom()[..100]));
        Assert.Throws<NotSupportedException>(() => Cartridge.Load(Rom(0x10)));
        var nes2 = Rom(); nes2[7] = 8;
        Assert.Throws<NotSupportedException>(() => Cartridge.Load(nes2));
    }
    [Theory] [InlineData(0, 0x2400)] [InlineData(1, 0x2800)]
    public void NametableMirroring(byte flags, int mirror)
    {
        var ppu = new Ppu(Cartridge.Load(Rom(flags)));
        ppu.WriteMemory(0x2000, 0x91);
        Assert.Equal(0x91, ppu.ReadMemory(mirror));
        Assert.Equal(0x91, ppu.ReadMemory(0x3000));
        ppu.WriteMemory(0x3f10, 0x22); Assert.Equal(0x22, ppu.ReadMemory(0x3f00));
    }
    [Fact] public void ChrRamWritableChrRomReadOnly()
    {
        var ram = Cartridge.Load(Rom()); ram.WriteChr(4, 12); Assert.Equal(12, ram.ReadChr(4));
        var rom = Cartridge.Load(Rom(chr: 1)); rom.WriteChr(4, 12); Assert.Equal(0, rom.ReadChr(4));
    }
    [Fact] public void ControllerSerializesAndReturnsOnesAfterEightBits()
    {
        var c = new Controller { Pressed = Buttons.A | Buttons.Start | Buttons.Right };
        c.Write(1); Assert.Equal(1, c.Read()); c.Write(0);
        c.Pressed = Buttons.None;
        Assert.Equal(new byte[] { 1, 0, 0, 1, 0, 0, 0, 1, 1, 1 }, Enumerable.Range(0, 10).Select(_ => c.Read()).ToArray());
    }
    [Fact] public void PpuDataIsBufferedAndStatusClearsNmi()
    {
        var p = new Ppu(Cartridge.Load(Rom()));
        p.WriteMemory(0x2000, 0x42);
        p.WriteRegister(6, 0x20); p.WriteRegister(6, 0);
        Assert.Equal(0, p.ReadRegister(7)); Assert.Equal(0x42, p.ReadRegister(7));
        p.WriteRegister(0, 0x80);
        for (int i = 0; i < 241 * 341 + 2; i++) p.Tick();
        Assert.True(p.Nmi); Assert.Equal(0x80, p.ReadRegister(2) & 0x80); Assert.False(p.Nmi);
    }
    [Theory] [InlineData(0, 513)] [InlineData(1, 514)]
    public void DmaTransfersOamAndStallsCpu(int parity, int cycles)
    {
        var m = new NesMachine(Cartridge.Load(Rom()));
        m.RunCycles(parity);
        for (int i = 0; i < 256; i++) m.Write((ushort)(0x200 + i), (byte)i);
        m.Write(0x2003, 0xf0); m.Write(0x4014, 2);
        int ticks = 0; while (m.DmaActive) { m.Tick(); ticks++; }
        Assert.Equal(cycles, ticks);
        for (int i = 0; i < 256; i++) Assert.Equal((byte)i, m.Ppu.Oam[(0xf0 + i) & 255]);
    }
    [Fact] public void CpuExecutesProgramAndDecimalFlagDoesNotEnableBcd()
    {
        // SED; LDA #$09; CLC; ADC #$01; STA $00; PHP; PLA; STA $01; JMP $800e
        byte[] code = [0xf8, 0xa9, 9, 0x18, 0x69, 1, 0x85, 0, 0x08, 0x68, 0x85, 1, 0x4c, 12, 0x80];
        var rom = Rom(); code.CopyTo(rom, 16);
        var m = new NesMachine(Cartridge.Load(rom)); m.RunCycles(80);
        Assert.Equal(10, m.Read(0)); Assert.NotEqual(0, m.Read(1) & 8);
        Assert.Equal(240, m.Ppu.Dot);
        // The shared NMOS core must still execute decimal arithmetic.
        var bus = new MemoryBus(); code.CopyTo(bus.Data, 0x8000); bus.Data[0xfffd] = 0x80;
        var cpu = new MOS6502 { Bus = bus }; cpu.Reset();
        for (int i = 0; i < 80; i++) cpu.Tick();
        Assert.Equal(0x10, bus.Data[0]);
    }
    sealed class MemoryBus : IBus
    {
        public byte[] Data { get; } = new byte[65536];
        public byte Read(ushort a) => Data[a];
        public void Write(ushort a, byte v) => Data[a] = v;
    }
}
