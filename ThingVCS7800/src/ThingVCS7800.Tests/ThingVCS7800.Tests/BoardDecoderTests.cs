using ThingVCS;
using ThingVCS7800.A7800;
namespace ThingVCS7800.Tests;

public class BoardDecoderTests
{
    [Theory][InlineData(0)][InlineData(0x100)][InlineData(0x200)][InlineData(0x300)]
    public void TiaAndMariaMirrorsUseSameChips(int mirror)
    {
        var host = new DevelopmentComputer();
        host.Board.Write((ushort)(mirror + 0x3c), 0x5a);
        Assert.Equal(0x5a, Assert.IsType<Maria>(host.Board.MariaSocket.Chip).Control);
        host.Board.Write((ushort)(mirror + 0x24), 0);
        Assert.True(host.Board.MariaSocket.Chip!.WaitSync);
        host.Reset();
        Assert.Equal(host.Board.Read(0x28), host.Board.Read((ushort)(mirror + 0x28)));
        host.Board.Write((ushort)(mirror + 1), 0x40);
        Assert.Equal(host.Board.Read(0xc), host.Board.Read((ushort)(mirror + 0xc)));
        Assert.Equal(BoardChipSelect.Tia, BoardAddressDecoder.Decode((ushort)(mirror + 0xc)).Chip);
        Assert.True(BoardAddressDecoder.Decode((ushort)(mirror + 0xc)).Slow);
        Assert.False(BoardAddressDecoder.Decode((ushort)(mirror + 0x28)).Slow);
    }
    [Fact] public void AllRiotRegisterAliasesPreserveRegisterSelect()
    {
        var host = new DevelopmentComputer();
        for (int mask = 0; mask < 8; mask++)
        {
            int mirror = ((mask & 1) * 0x20) | ((mask & 2) * 0x20) | ((mask & 4) * 0x40);
            host.Board.Write((ushort)(0x281 | mirror), (byte)(mask + 1));
            Assert.Equal(mask + 1, host.Board.Read(0x281));
            for (int register = 0; register < 32; register++)
            {
                var decoded = BoardAddressDecoder.Decode((ushort)((0x280 + register) | mirror));
                Assert.Equal(BoardChipSelect.RiotRegisters, decoded.Chip);
                Assert.Equal(register, decoded.Offset); Assert.True(decoded.Slow);
            }
        }
    }
    [Fact] public void RiotRamLivesInChipAndHasOnlyTheDocumentedPageMirror()
    {
        var host = new DevelopmentComputer();
        for (int i = 0; i < 128; i++) host.Board.Write((ushort)(0x480 + i), (byte)i);
        for (int i = 0; i < 128; i++)
        {
            Assert.Equal(i, host.Board.Read((ushort)(0x580 + i)));
            Assert.Equal(i, host.Board.RiotSocket.Chip!.ReadRam(i));
        }
        host.Board.Write(0x5ff, 0xaa); Assert.Equal(0xaa, host.Board.Read(0x4ff));
        host.Board.RiotSocket.Remove(); host.Board.Write(0x4ff, 0x55);
        Assert.Equal(0x55, host.Board.Read(0x4ff)); // Open bus, not a hidden RAM fallback.
        Assert.Equal(BoardChipSelect.None, BoardAddressDecoder.Decode(0x680).Chip);
    }
    [Fact] public void LowIoOverlayDoesNotModifyRamBehindIt()
    {
        var host = new DevelopmentComputer(); host.Board.Write(0x213c, 42);
        host.Board.Write(0x13c, 99);
        Assert.Equal(42, host.Board.Read(0x213c));
        Assert.Equal(99, Assert.IsType<Maria>(host.Board.MariaSocket.Chip).Control);
        Assert.Equal(BoardChipSelect.MainRam, BoardAddressDecoder.Decode(0x213c).Chip);
    }
    [Fact] public void CompleteAddressSpaceHasBoundedChipOffsetsAndNoSpeculativeRamMirrors()
    {
        for (int i = 0; i <= ushort.MaxValue; i++)
        {
            var decoded = BoardAddressDecoder.Decode((ushort)i);
            int max = decoded.Chip switch
            {
                BoardChipSelect.MainRam => 4095,
                BoardChipSelect.RiotRam => 127,
                BoardChipSelect.Tia or BoardChipSelect.Maria or BoardChipSelect.RiotRegisters => 31,
                BoardChipSelect.Cartridge => 65535,
                _ => 0
            };
            Assert.InRange(decoded.Offset, 0, max);
            if (i is >= 0x2800 and < 0x4000) Assert.Equal(BoardChipSelect.None, decoded.Chip);
        }
        Assert.Equal(BoardChipSelect.None, BoardAddressDecoder.Decode(0x240).Chip);
        Assert.Equal(BoardChipSelect.MainRam, BoardAddressDecoder.Decode(0x140).Chip);
    }
    [Theory][InlineData(0x10c)][InlineData(0x20c)][InlineData(0x30c)][InlineData(0x3e0)][InlineData(0x480)][InlineData(0x580)]
    public void CpuMirrorAccessUsesSlowSlot(int address)
    {
        var host = new DevelopmentComputer(); var rom = new byte[16384];
        byte[] code = [0xad, (byte)address, (byte)(address >> 8), 0x4c, 0, 0xc0];
        code.CopyTo(rom, 0); rom[0x3ffd] = 0xc0; host.Insert(rom);
        for (int i = 0; i < 100; i++) host.Board.Tick();
        Assert.True(host.Board.SlowCpuAccesses > 0);
        Assert.True(host.Board.MasterClocks > host.Board.Cycles * 4);
    }
}
