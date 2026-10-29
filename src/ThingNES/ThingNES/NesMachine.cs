using ThingRetroEmu.Chips;

namespace ThingNES;

public sealed class NesMachine : IBus
{
    readonly byte[] ram = new byte[2048];
    byte openBus;
    int dmaPage = -1, dmaIndex, dmaDummy;
    bool dmaRead;
    byte dmaValue;
    int dmcStall;
    public bool DmcDmaActive => dmcStall > 0;
    public long DmcDmaCycles { get; private set; }
    public Cartridge Cartridge { get; }
    public Ricoh2A03 Cpu { get; } = new();
    public Ppu Ppu { get; }
    public Apu Apu { get; } = new();
    public Controller Controller1 { get; } = new();
    public Controller Controller2 { get; } = new();
    public long CpuCycles { get; private set; }
    public bool DmaActive => dmaPage >= 0;

    public NesMachine(Cartridge cartridge)
    {
        Cartridge = cartridge;
        Ppu = new(cartridge);
        Cpu.Bus = this;
        Cpu.NmiInput = () => Ppu.Nmi;
        Cpu.IrqInput = () => Apu.Irq;
        Cpu.Reset();
    }
    public byte Read(ushort address)
    {
        byte value = address switch
        {
            < 0x2000 => ram[address & 0x7ff],
            < 0x4000 => Ppu.ReadRegister(address & 7),
            0x4015 => Apu.ReadStatus(),
            0x4016 => (byte)((openBus & 0xe0) | Controller1.Read()),
            0x4017 => (byte)((openBus & 0xe0) | Controller2.Read()),
            >= 0x6000 => Cartridge.ReadCpu(address),
            _ => openBus
        };
        return openBus = value;
    }
    public void Write(ushort address, byte value)
    {
        openBus = value;
        if (address < 0x2000) ram[address & 0x7ff] = value;
        else if (address < 0x4000) Ppu.WriteRegister(address & 7, value);
        else if (address == 0x4014)
        {
            dmaPage = value; dmaIndex = 0; dmaRead = true;
            dmaDummy = 1 + (int)(CpuCycles & 1);
        }
        else if (address == 0x4016) { Controller1.Write(value); Controller2.Write(value); }
        else if (address is >= 0x4000 and <= 0x4013 or 0x4015 or 0x4017) Apu.Write(address, value);
        else if (address >= 0x6000) Cartridge.WriteCpu(address, value);
    }
    public void Tick()
    {
        // Functional fixed four-cycle DMC fetch. Exact RDY/write arbitration and OAM overlap are deferred.
        if (dmcStall == 0 && Apu.Dmc.DmaAddress is not null) dmcStall = 4;
        if (dmcStall > 0)
        {
            DmcDmaCycles++;
            if (--dmcStall == 0 && Apu.Dmc.DmaAddress is ushort sampleAddress)
                Apu.Dmc.SupplySample(Read(sampleAddress));
        }
        else if (!DmaActive) Cpu.Tick();
        else if (dmaDummy > 0) dmaDummy--;
        else if (dmaRead) { dmaValue = Read((ushort)((dmaPage << 8) | dmaIndex)); dmaRead = false; }
        else
        {
            Ppu.WriteRegister(4, dmaValue); dmaRead = true;
            if (++dmaIndex == 256) dmaPage = -1;
        }
        Apu.Tick(); // APU keeps running while OAM DMA stalls the CPU.
        for (int i = 0; i < 3; i++) Ppu.Tick();
        CpuCycles++;
    }
    // Advance to the next PPU frame boundary; may start mid-frame and overshoot by at most two dots.
    public int RunFrame()
    {
        long target = Ppu.Frames + 1;
        int elapsed = 0;
        while (Ppu.Frames < target) { Tick(); elapsed++; }
        return elapsed;
    }

    public void RunCycles(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        for (int i = 0; i < count; i++) Tick();
    }
}
