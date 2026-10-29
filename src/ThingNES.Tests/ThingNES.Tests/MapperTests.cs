using ThingNES;

namespace ThingNES.Tests;

public class MapperTests
{
    static byte[] Image(int mapper, int prgBanks, int chrBanks)
    {
        var rom = new byte[16 + prgBanks * 16384 + chrBanks * 8192];
        "NES\u001a"u8.CopyTo(rom); rom[4] = (byte)prgBanks; rom[5] = (byte)chrBanks; rom[6] = (byte)(mapper << 4);
        for (int bank = 0; bank < prgBanks; bank++)
        { Array.Fill(rom, (byte)255, 16 + bank * 16384, 16384); rom[16 + bank * 16384] = (byte)bank; }
        for (int bank = 0; bank < chrBanks; bank++) Array.Fill(rom, (byte)(bank + 1), 16 + prgBanks * 16384 + bank * 8192, 8192);
        return rom;
    }
    [Fact] public void UxromSwitchesLowBankAndKeepsLastBankFixed()
    {
        var c = Cartridge.Load(Image(2, 4, 0));
        Assert.Equal("UxROM", c.MapperName); Assert.Equal(0, c.ReadCpu(0x8000)); Assert.Equal(3, c.ReadCpu(0xc000));
        c.WriteCpu(0x8100, 2); Assert.Equal(2, c.ReadCpu(0x8000)); Assert.Equal(3, c.ReadCpu(0xc000));
        c.WriteCpu(0xffff, 255); Assert.Equal(3, c.SelectedBank); Assert.Equal(3, c.ReadCpu(0x8000));
        c.WriteChr(20, 99); Assert.Equal(99, c.ReadChr(20));
    }
    [Fact] public void BusConflictUsesCurrentlyMappedRomByte()
    {
        var c = Cartridge.Load(Image(2, 4, 0)); c.WriteCpu(0x8000, 3); Assert.Equal(0, c.SelectedBank);
        c.WriteCpu(0x8100, 2); c.WriteCpu(0x8000, 3); Assert.Equal(2, c.SelectedBank);
        var noConflict = Cartridge.Load(Image(2, 4, 0), busConflicts: false);
        noConflict.WriteCpu(0x8000, 3); Assert.Equal(3, noConflict.SelectedBank); Assert.False(noConflict.BusConflicts);
    }
    [Theory] [InlineData(1)] [InlineData(2)] [InlineData(4)]
    public void CnromSelectsChrWithWiredBankMask(int chrBanks)
    {
        var c = Cartridge.Load(Image(3, 1, chrBanks)); Assert.Equal(1, c.ReadChr(0));
        c.WriteCpu(0x8100, 255); Assert.Equal(chrBanks - 1, c.SelectedBank); Assert.Equal(chrBanks, c.ReadChr(0x1fff));
        c.WriteChr(0, 99); Assert.Equal(chrBanks, c.ReadChr(0));
        Assert.Equal(c.ReadCpu(0x8000), c.ReadCpu(0xc000));
    }
    [Fact] public void CnromBusConflictsDoNotModifyPrg()
    {
        var c = Cartridge.Load(Image(3, 2, 4)); c.WriteCpu(0x8000, 3);
        Assert.Equal(0, c.SelectedBank); Assert.Equal(0, c.ReadCpu(0x8000));
        c.WriteCpu(0xc000, 3); Assert.Equal(1, c.SelectedBank); Assert.Equal(2, c.ReadChr(0));
    }
    [Theory] [InlineData(2, 1, 0)] [InlineData(2, 3, 0)] [InlineData(2, 4, 1)] [InlineData(3, 1, 0)] [InlineData(3, 4, 4)] [InlineData(3, 1, 3)]
    public void UnsupportedBoardLayoutsAreRejected(int mapper, int prg, int chr)
        => Assert.Throws<InvalidDataException>(() => Cartridge.Load(Image(mapper, prg, chr)));
    [Fact] public void TrainerIsCopiedWithoutShiftingPrgIncorrectly()
    {
        var original = Image(2, 4, 0); var rom = new byte[original.Length + 512];
        original.AsSpan(0, 16).CopyTo(rom); rom[6] |= 4;
        Array.Fill(rom, (byte)0x56, 16, 512); original.AsSpan(16).CopyTo(rom.AsSpan(528));
        var c = Cartridge.Load(rom); Assert.Equal(0x56, c.ReadCpu(0x7000)); Assert.Equal(0x56, c.ReadCpu(0x71ff));
        Assert.Equal(3, c.ReadCpu(0xc000));
        Assert.Throws<InvalidDataException>(() => Cartridge.Load(rom[..^1]));
    }
    [Fact] public void CpuExecutesAcrossUxromBankSwitch()
    {
        var rom = Image(2, 4, 0); int last = 16 + 3 * 16384;
        byte[] fixedCode = [0xa9, 1, 0x8d, 0, 0xc1, 0x4c, 0, 0x80]; fixedCode.CopyTo(rom, last);
        byte[] switchedCode = [0xa9, 0x55, 0x8d, 0, 0x60, 0x4c, 5, 0x80]; switchedCode.CopyTo(rom, 16 + 16384);
        rom[last + 0x3ffc] = 0; rom[last + 0x3ffd] = 0xc0;
        var m = new NesMachine(Cartridge.Load(rom)); m.RunCycles(60);
        Assert.Equal(1, m.Cartridge.SelectedBank); Assert.Equal(0x55, m.Read(0x6000));
    }
    [Fact] public void PpuReadsSwitchedCnromThroughExistingCartridgeWiring()
    {
        var m = new NesMachine(Cartridge.Load(Image(3, 2, 4)));
        Assert.Equal(1, m.Ppu.ReadMemory(0x1000)); m.Write(0x8100, 2); Assert.Equal(3, m.Ppu.ReadMemory(0x1000));
        m.Write(0x6000, 0x55); Assert.Equal(0x55, m.Read(0x6000));
    }
}
