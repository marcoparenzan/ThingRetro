using ThingVCS;
using ThingVCS7800.A7800;
namespace ThingVCS7800.Tests;
public class BootAnd2600Tests
{
    [Theory][InlineData(4096)][InlineData(16384)]
    public void BiosOverlayCanBeDisabledLockedAndReenabledOnlyByReset(int size)
    {
        var board=new Board { HardwareBoot=true }; var bios=Enumerable.Repeat((byte)0xaa,size).ToArray();
        board.BiosSocket.Insert(new BootRom(bios)); board.CartridgeSocket.Insert(new LinearCartridge(new byte[16384])); board.Reset();
        Assert.Equal(0xaa,board.Read(0xffff)); Assert.False(board.ControlLocked);
        board.Write(1,7); Assert.True(board.ControlLocked); Assert.False(board.BiosEnabled); Assert.False(board.Mode2600);
        Assert.Equal(0,board.Read(0xffff)); board.Write(1,2); Assert.False(board.BiosEnabled);
        board.Reset(); Assert.True(board.BiosEnabled); Assert.False(board.ControlLocked); Assert.Equal(0xaa,board.Read(0xffff));
    }
    [Fact] public void CpuBootsBiosThenRamTrampolineDisablesOverlayAndEntersCart()
    {
        var host=new DevelopmentComputer(); var cart=new byte[16384];
        byte[] game=[0xa9,42,0x8d,0x0f,0x18,0x4c,5,0xc0]; game.CopyTo(cart,0); cart[0x3ffd]=0xc0; host.Insert(cart);
        byte[] trampoline=[0xa9,7,0x8d,1,0,0x4c,0,0xc0];
        for(int i=0;i<trampoline.Length;i++) host.Board.Write((ushort)(0x1800+i),trampoline[i]);
        var bios=new byte[4096]; bios[0]=0x4c; bios[1]=0; bios[2]=0x18; bios[0xffd]=0xf0;
        host.InstallBios(bios); for(int i=0;i<300;i++) host.Board.Tick();
        Assert.Equal(42,host.Board.Read(0x180f)); Assert.True(host.Board.ControlLocked); Assert.False(host.Board.BiosEnabled);
    }
    [Fact] public void UnlockedTiaWritesLatchControlAndLockedWritesStillReachAudio()
    {
        var host=new DevelopmentComputer(); host.Board.HardwareBoot=true; host.Reset();
        host.Board.Write(0x101,5); Assert.True(host.Board.Mode2600); Assert.True(host.Board.ControlLocked);
        host.Board.Write(0x19,15); Assert.Equal(.5,Assert.IsType<TiaInputs>(host.Board.TiaInputSocket.Chip).AudioLevel);
        host.Board.Write(1,7); Assert.True(host.Board.Mode2600);
    }
    [Theory][InlineData(2048)][InlineData(4096)]
    public void VcsModeHas13BitBusAndRiotStackMirrors(int size)
    {
        var host=new DevelopmentComputer(); var rom=Enumerable.Repeat((byte)0x55,size).ToArray(); host.Insert2600(rom);
        Assert.True(host.Board.Mode2600); Assert.Equal(0x55,host.Board.Read(0xffff)); Assert.Equal(0x55,host.Board.Read(0x1fff));
        host.Board.Write(0x80,42); Assert.Equal(42,host.Board.Read(0x180)); Assert.Equal(42,host.Board.Read(0x2180));
        Assert.Equal(42,host.Board.RiotSocket.Chip!.ReadRam(0));
        long before=host.Board.MasterClocks; host.Board.Tick(); Assert.Equal(6,host.Board.MasterClocks-before);
        Assert.Equal(0,host.Board.MariaSocket.Chip!.Frames);
        host.Insert(new byte[16384]); Assert.False(host.Board.Mode2600);
    }
    [Fact] public void VcsSyntheticCpuUsesRiotRamAndNoMariaBusOwnership()
    {
        var host=new DevelopmentComputer(); var rom=new byte[4096];
        byte[] program=[0xa9,42,0x85,0x80,0x4c,4,0xf0]; program.CopyTo(rom,0); rom[0xffd]=0xf0; host.Insert2600(rom);
        for(int i=0;i<100;i++) host.Board.Tick(); Assert.Equal(42,host.Board.Read(0x80));
        Assert.Equal(600,host.Board.MasterClocks); Assert.Equal(0,host.Board.MariaSocket.Chip!.Frames);
    }
    [Fact] public void VcsAddressDecoderRepeatsEveryEightKiBAndNeverSelectsMaria()
    {
        for(int address=0;address<65536;address++)
        {
            var selected=BoardAddressDecoder.Decode2600((ushort)address);
            Assert.Equal(BoardAddressDecoder.Decode2600((ushort)(address&0x1fff)),selected);
            Assert.NotEqual(BoardChipSelect.Maria,selected.Chip);
            Assert.NotEqual(BoardChipSelect.MainRam,selected.Chip);
        }
    }
    [Fact] public void InvalidBiosDoesNotReplaceInstalledFirmware()
    {
        var host=new DevelopmentComputer(); host.InstallBios(new byte[4096]); var previous=host.Board.BiosSocket.Chip;
        Assert.Throws<ArgumentException>(()=>host.InstallBios(new byte[100])); Assert.Same(previous,host.Board.BiosSocket.Chip);
        host.RemoveBios(); Assert.True(host.Board.BiosSocket.IsEmpty); Assert.False(host.Board.BiosEnabled);
    }
}
