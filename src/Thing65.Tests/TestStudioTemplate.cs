using ThingMOS.Asm;
using Thing65.C65;

namespace Thing65.Tests;

// The program of a new studio project must run in C65 mode: assembled for the 4510 below $2000 and started with SYS 6144
[TestClass]
public sealed class StudioTemplateTests
{
    const string Source = """
        !cpu 4510
        * = $1800
        start:  LDA #6
                STA $D021
        loop:   LDA $D012
                BNE loop
        wait:   LDA $D012
                BEQ wait
                INC $D020
                JMP loop
        """;

    [TestMethod]
    [Timeout(300_000)]
    public void ProgramBelow2000_ShouldRunWithSys()
    {
        string rom = Path.Combine(AppContext.BaseDirectory, "roms", "c-65-19910828.rom");
        if (!File.Exists(rom)) Assert.Inconclusive("C65 ROM missing");
        var board = new BoardC65(new SystemRom128(File.ReadAllBytes(rom)), startClock: false);
        board.RunCycles(9_000_000);

        var result = Assembler.Assemble(Source);
        Assert.AreEqual(0x1800, result.Origin);
        for (int i = 0; i < result.Bytes.Length; i++) board.CpuWrite((ushort)(result.Origin + i), result.Bytes[i]);
        board.TypeText("SYS 6144\r");
        board.RunCycles(2_000_000);

        Assert.AreEqual(6, board.CpuRead(0xD021) & 0x0F, "background");
    }
}
