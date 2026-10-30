using ThingMOS;
using ThingVCS;
using ThingVCS7800.A7800;
using ThingRetroEmu.Chips;
namespace ThingVCS7800.Tests;

public class SallyPhaseTests
{
    sealed class Memory : IBus
    {
        public byte[] Data = new byte[65536];
        public List<BusCycle> Traffic = [];
        public byte Read(ushort address) { Traffic.Add(new(address, false)); return Data[address]; }
        public void Write(ushort address, byte value) { Traffic.Add(new(address, true)); Data[address] = value; }
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void PreviewMatchesActualCyclesForAllOpcodesWithoutSideEffects(bool interrupt)
    {
        for (int opcode = 0; opcode < 256; opcode++)
        {
            var memory = new Memory(); Array.Fill(memory.Data, (byte)0xea);
            memory.Data[0xc000] = (byte)opcode; memory.Data[0xc001] = 0xff; memory.Data[0xc002] = 0;
            memory.Data[0xfffc] = 0; memory.Data[0xfffd] = 0xc0;
            bool nmi = false;
            var cpu = new Sally { Bus = memory, NmiInput = () => nmi };
            cpu.Reset(); cpu.A = 0xaa; cpu.X = cpu.Y = 1;
            for (int cycle = 0; cycle < 32; cycle++)
            {
                memory.Traffic.Clear(); nmi = interrupt && cycle is 8 or 9;
                var pc = cpu.PC; var sp = cpu.SP; var status = cpu.Status;
                var expected = cpu.NextBusCycle;
                Assert.Equal(expected, cpu.NextBusCycle);
                Assert.Empty(memory.Traffic);
                Assert.Equal(pc, cpu.PC); Assert.Equal(sp, cpu.SP); Assert.Equal(status, cpu.Status);
                cpu.Tick();
                if (expected is { } access) Assert.Equal(access, Assert.Single(memory.Traffic));
                else Assert.Empty(memory.Traffic);
            }
        }
    }
    sealed class PhaseBus(Board board) : IBus
    {
        public List<(ushort Address, int Phase, int Length, long Clock)> Traffic = [];
        void Observe(ushort address)
        {
            Assert.True(board.Phi2);
            Assert.Equal(board.CpuSlotClocks - 1, board.CpuMasterPhase);
            Assert.False(((Sally)board.CpuSocket.Chip!).HaltInput());
            Traffic.Add((address, board.CpuMasterPhase, board.CpuSlotClocks, board.MasterClocks));
        }
        public byte Read(ushort address) { Observe(address); return board.Read(address); }
        public void Write(ushort address, byte value) { Observe(address); board.Write(address, value); }
    }
    [Fact] public void SlowAccessIsSelectedBeforeBusOperationAndCommitsAtPhi2End()
    {
        var host = new DevelopmentComputer();
        var rom = new byte[16384];
        byte[] code = [0xad,0x80,0x02,0x8d,0x10,0x00,0xad,0x00,0x18,0x4c,0x00,0xc0];
        code.CopyTo(rom, 0); rom[^3] = 0xc0;
        host.Insert(rom);
        var bus = new PhaseBus(host.Board); host.Board.CpuSocket.Chip!.Bus = bus;
        for (int i = 0; i < 100; i++) host.Board.Tick();
        Assert.Contains(bus.Traffic, r => r.Address == 0x0280 && r.Length == 6 && r.Phase == 5);
        Assert.Contains(bus.Traffic, r => r.Address == 0x0010 && r.Length == 6 && r.Phase == 5);
        Assert.Contains(bus.Traffic, r => r.Address == 0x1800 && r.Length == 4 && r.Phase == 3);
        Assert.Equal(host.Board.MasterClocks, ((Maria)host.Board.MariaSocket.Chip!).MasterClocks);
        Assert.False(host.Board.Phi2); Assert.Equal(-1, host.Board.CpuMasterPhase);
    }
    [Fact] public void ReleaseAtPhi1RestoresFullSlowSlotBeforePendingRead()
    {
        var host = new DevelopmentComputer(); var rom = new byte[16384];
        byte[] code = [0xad,0x80,0x02,0x4c,0x00,0xc0]; code.CopyTo(rom, 0); rom[^3] = 0xc0;
        host.Insert(rom); var cpu = (Sally)host.Board.CpuSocket.Chip!;
        while (cpu.NextBusCycle?.Address != 0x0280) host.Board.Tick();
        bool halt = true; cpu.HaltInput = () => halt;
        cpu.NmiInput = () => { if (host.Board.CpuMasterPhase == 0) halt = false; return false; };
        var bus = new PhaseBus(host.Board); cpu.Bus = bus;
        long before = host.Board.MasterClocks; host.Board.Tick();
        Assert.Equal(6, host.Board.MasterClocks - before);
        Assert.Equal((ushort)0x0280, Assert.Single(bus.Traffic).Address);
        Assert.Equal(5, bus.Traffic[0].Phase);
    }
    [Fact] public void DmaRequestDuringSlowPhi2WaitsForNextCycleBoundary()
    {
        var host = new DevelopmentComputer(); var rom = new byte[16384];
        byte[] code = [0xad,0x80,0x02,0x4c,0x00,0xc0]; code.CopyTo(rom, 0); rom[^3] = 0xc0;
        host.Insert(rom); var cpu = (Sally)host.Board.CpuSocket.Chip!;
        var maria = (Maria)host.Board.MariaSocket.Chip!;
        for (int i = 0; i < 10000 && !(maria.Scanline == 16 && maria.Dot is >= 30 and <= 100 && cpu.NextBusCycle?.Address == 0x0280); i++)
            host.Board.Tick();
        Assert.Equal((ushort)0x0280, cpu.NextBusCycle!.Value.Address);
        Assert.Equal(16, maria.Scanline);
        cpu.NmiInput = () => { if (host.Board.CpuMasterPhase == 3) maria.WriteRegister(28, 0x40); return false; };
        var bus = new PhaseBus(host.Board); cpu.Bus = bus;
        host.Board.Tick();
        Assert.False(maria.DmaBusy);
        Assert.Equal((ushort)0x0280, Assert.Single(bus.Traffic).Address);
        host.Board.Tick(); Assert.True(maria.DmaBusy);
        Assert.Single(bus.Traffic);
    }
    [Fact] public void DmaBoundaryPinDisconnectsWithMaria()
    {
        var host = new DevelopmentComputer(); var maria = (Maria)host.Board.MariaSocket.Chip!;
        Assert.NotNull(maria.BusBoundaryInput);
        Assert.False(maria.BusBoundaryInput!());
        host.Board.MariaSocket.Remove(); Assert.Null(maria.BusBoundaryInput);
    }
}
