using ThingNES;

namespace ThingNES.Tests;

public class DiagnosticTests
{
    static NesMachine Program(byte result, bool signature = true)
    {
        var rom = new byte[16400]; "NES\u001a"u8.CopyTo(rom); rom[4] = 1;
        var code = new List<byte>();
        void Store(ushort address, byte value) => code.AddRange([0xa9, value, 0x8d, (byte)address, (byte)(address >> 8)]);
        Store(0x6000, 0x80);
        if (signature) { Store(0x6001, 0xde); Store(0x6002, 0xb0); Store(0x6003, 0x61); }
        Store(0x6004, (byte)'O'); Store(0x6005, (byte)'K'); Store(0x6006, 0);
        Store(0x6000, result);
        int loop = 0x8000 + code.Count; code.AddRange([0x4c, (byte)loop, (byte)(loop >> 8)]);
        code.CopyTo(rom, 16); rom[16 + 0x3ffd] = 0x80;
        return new NesMachine(Cartridge.Load(rom));
    }
    [Theory] [InlineData(0, DiagnosticOutcome.Passed, 0)] [InlineData(7, DiagnosticOutcome.Failed, 2)] [InlineData(0x81, DiagnosticOutcome.ResetRequested, 4)]
    public void ExecutesReal6502ProgramAndReportsProtocol(byte code, DiagnosticOutcome outcome, int exit)
    {
        var result = DiagnosticRunner.Run(Program(code), 1000);
        Assert.Equal(outcome, result.Outcome); Assert.Equal(code, result.Code); Assert.Equal(exit, result.ExitCode);
        Assert.Equal("OK", result.Message); Assert.True(result.SignatureSeen); Assert.InRange(result.Cycles, 1, 999);
    }
    [Fact] public void RicohLxaLoadsImmediateIntoBothRegisters()
    {
        var rom = new byte[16400]; "NES\u001a"u8.CopyTo(rom); rom[4] = 1; rom[16 + 0x3ffd] = 0x80;
        byte[] code = [0xa9, 0, 0xab, 0x11, 0x85, 0, 0x86, 1, 0x4c, 8, 0x80]; code.CopyTo(rom, 16);
        var machine = new NesMachine(Cartridge.Load(rom)); machine.RunCycles(40);
        Assert.Equal(0x11, machine.Read(0)); Assert.Equal(0x11, machine.Read(1));
    }
    [Fact] public void MissingSignatureNeverReportsFalsePass()
    {
        var result = DiagnosticRunner.Run(Program(0, false), 200);
        Assert.Equal(DiagnosticOutcome.TimedOut, result.Outcome); Assert.Equal(200, result.Cycles);
        Assert.False(result.SignatureSeen); Assert.Equal("", result.Message); Assert.Null(result.Code);
    }
    [Fact] public void RunningStatusTimesOutWithMessage()
    {
        var result = DiagnosticRunner.Run(Program(0x80), 200);
        Assert.Equal(DiagnosticOutcome.TimedOut, result.Outcome); Assert.True(result.SignatureSeen); Assert.Equal("OK", result.Message);
    }
    [Fact] public void CancellationAndInvalidBudget()
    {
        var machine = Program(0); using var cts = new CancellationTokenSource(); cts.Cancel();
        var result = DiagnosticRunner.Run(machine, 100, cts.Token);
        Assert.Equal(DiagnosticOutcome.Cancelled, result.Outcome); Assert.Equal(0, result.Cycles);
        Assert.Throws<ArgumentOutOfRangeException>(() => DiagnosticRunner.Run(machine, 0));
    }
    [Fact] public void TextIsBoundedAndTerminalControlsAreSanitized()
    {
        var machine = Program(0x80); machine.RunCycles(200);
        machine.Cartridge.WriteCpu(0x6004, 27); machine.Cartridge.WriteCpu(0x6005, 10);
        machine.Cartridge.WriteCpu(0x6000, 0);
        var result = DiagnosticRunner.Run(machine, 10);
        Assert.Equal("?\n", result.Message);
    }
}
