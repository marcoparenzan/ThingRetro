using static Thing64.Tests.TestHelpers;

namespace Thing64.Tests;

[TestClass]
public sealed class MOS6510Tests
{
    #region Decimal mode

    [TestMethod]
    [DataRow(0x09, 0x01, false, 0x10, false)]
    [DataRow(0x99, 0x01, false, 0x00, true)]
    [DataRow(0x58, 0x46, false, 0x04, true)]
    [DataRow(0x12, 0x34, true, 0x47, false)]
    [DataRow(0x45, 0x45, false, 0x90, false)]
    public void Adc_DecimalMode_ShouldAddBcd(int a, int operand, bool carryIn, int expected, bool carryOut)
    {
        var board = NewBoard();
        board.Load(0xC000, 0xF8, (byte)(carryIn ? 0x38 : 0x18), 0xA9, (byte)a, 0x69, (byte)operand); // SED; SEC/CLC; LDA #a; ADC #operand

        for (int i = 0; i < 4; i++) board.StepInstruction();

        Assert.AreEqual(expected, (int)board.Cpu.A);
        Assert.AreEqual(carryOut, (board.Cpu.Status & FLAG_C) != 0);
    }

    [TestMethod]
    [DataRow(0x50, 0x01, true, 0x49, true)]
    [DataRow(0x00, 0x01, true, 0x99, false)]
    [DataRow(0x46, 0x12, true, 0x34, true)]
    [DataRow(0x40, 0x13, false, 0x26, true)]
    public void Sbc_DecimalMode_ShouldSubtractBcd(int a, int operand, bool carryIn, int expected, bool carryOut)
    {
        var board = NewBoard();
        board.Load(0xC000, 0xF8, (byte)(carryIn ? 0x38 : 0x18), 0xA9, (byte)a, 0xE9, (byte)operand); // SED; SEC/CLC; LDA #a; SBC #operand

        for (int i = 0; i < 4; i++) board.StepInstruction();

        Assert.AreEqual(expected, (int)board.Cpu.A);
        Assert.AreEqual(carryOut, (board.Cpu.Status & FLAG_C) != 0);
    }

    [TestMethod]
    public void Adc_BinaryMode_ShouldSetOverflow()
    {
        var board = NewBoard();
        board.Load(0xC000, 0x18, 0xA9, 0x7F, 0x69, 0x01); // CLC; LDA #$7F; ADC #$01

        for (int i = 0; i < 3; i++) board.StepInstruction();

        Assert.AreEqual(0x80, (int)board.Cpu.A);
        Assert.AreNotEqual(0, board.Cpu.Status & FLAG_V);
        Assert.AreNotEqual(0, board.Cpu.Status & FLAG_N);
    }

    #endregion

    #region Stack and subroutines

    [TestMethod]
    public void Jsr_ShouldPushAddressOfLastByte_AndRtsShouldReturnAfterIt()
    {
        var board = NewBoard();
        board.Load(0xC000, 0x20, 0x00, 0xC1, 0xEA); // JSR $C100; NOP
        board.Poke(0xC100, 0x60);                   // RTS
        byte sp = board.Cpu.SP;

        board.StepInstruction();

        Assert.AreEqual(0xC100, (int)board.Cpu.PC);
        Assert.AreEqual(0xC0, (int)board.Peek((ushort)(0x0100 + sp)));       // high byte
        Assert.AreEqual(0x02, (int)board.Peek((ushort)(0x0100 + sp - 1)));   // low byte: $C002 = last byte of JSR

        board.StepInstruction();

        Assert.AreEqual(0xC003, (int)board.Cpu.PC);
    }

    [TestMethod]
    public void Rts_ShouldJumpToPushedAddressPlusOne()
    {
        // The trick BASIC uses to run its commands: push (target - 1), then RTS
        var board = NewBoard();
        board.Load(0xC000, 0xA9, 0xC1, 0x48, 0xA9, 0xFF, 0x48, 0x60); // LDA #$C1; PHA; LDA #$FF; PHA; RTS

        for (int i = 0; i < 5; i++) board.StepInstruction();

        Assert.AreEqual(0xC200, (int)board.Cpu.PC);
    }

    [TestMethod]
    public void Brk_ShouldPushAddressAfterPaddingByte_WithBreakFlag()
    {
        var board = NewBoard();
        board.Load(0xC000, 0x00, 0xEA); // BRK; padding
        byte sp = board.Cpu.SP;

        board.StepInstruction();

        Assert.AreEqual(0xC0, (int)board.Peek((ushort)(0x0100 + sp)));
        Assert.AreEqual(0x02, (int)board.Peek((ushort)(0x0100 + sp - 1)));
        Assert.AreNotEqual(0, board.Peek((ushort)(0x0100 + sp - 2)) & FLAG_B);
        Assert.AreEqual(board.Peek(0xFFFE) | board.Peek(0xFFFF) << 8, (int)board.Cpu.PC);
    }

    #endregion

    #region Addressing

    [TestMethod]
    public void LdxZeroPageY_ShouldReadIndexedAddress()
    {
        var board = NewBoard();
        board.Poke(0x0015, 0x42);
        board.Load(0xC000, 0xA0, 0x05, 0xB6, 0x10); // LDY #$05; LDX $10,Y

        board.StepInstruction();
        long cycles = board.CyclesOf(board.StepInstruction);

        Assert.AreEqual(0x42, (int)board.Cpu.X);
        Assert.AreEqual(4, cycles);
    }

    [TestMethod]
    public void StxZeroPageY_ShouldWrapInZeroPage()
    {
        var board = NewBoard();
        board.Load(0xC000, 0xA2, 0x99, 0xA0, 0x10, 0x96, 0xF8); // LDX #$99; LDY #$10; STX $F8,Y -> $08

        for (int i = 0; i < 3; i++) board.StepInstruction();

        Assert.AreEqual(0x99, (int)board.Peek(0x0008));
    }

    [TestMethod]
    public void JmpIndirect_ShouldReproducePageBoundaryBug()
    {
        var board = NewBoard();
        board.Poke(0xC1FF, 0x34);
        board.Poke(0xC100, 0x12); // the high byte is read from $C100, not from $C200
        board.Poke(0xC200, 0x56);
        board.Load(0xC000, 0x6C, 0xFF, 0xC1); // JMP ($C1FF)

        board.StepInstruction();

        Assert.AreEqual(0x1234, (int)board.Cpu.PC);
    }

    #endregion

    #region Timing

    [TestMethod]
    [DataRow(0x00, 4)]
    [DataRow(0x01, 5)]
    public void LdaAbsoluteX_ShouldAddCycleOnPageCross(int x, int expectedCycles)
    {
        var board = NewBoard();
        board.Load(0xC000, 0xA2, (byte)x, 0xBD, 0xFF, 0xC0); // LDX #x; LDA $C0FF,X

        board.StepInstruction();
        long cycles = board.CyclesOf(board.StepInstruction);

        Assert.AreEqual(expectedCycles, cycles);
    }

    [TestMethod]
    public void StaAbsoluteX_ShouldAlwaysTakeFiveCycles()
    {
        var board = NewBoard();
        board.Load(0xC000, 0xA2, 0x00, 0x9D, 0x00, 0xC3); // LDX #$00; STA $C300,X

        board.StepInstruction();
        long cycles = board.CyclesOf(board.StepInstruction);

        Assert.AreEqual(5, cycles);
    }

    [TestMethod]
    public void Branch_ShouldTakeTwoThreeOrFourCycles()
    {
        var board = NewBoard();
        // Page crossing is measured from the address following the branch ($C0FE -> $C103)
        board.Load(0xC0F8, 0xA9, 0x01, 0xF0, 0x00, 0xD0, 0x05); // LDA #$01; BEQ (not taken); BNE +5 (taken, crosses a page)
        board.Poke(0xC103, 0xD0, 0x00);                        // BNE +0 (taken, same page)

        board.StepInstruction();
        long notTaken = board.CyclesOf(board.StepInstruction);
        long takenCrossing = board.CyclesOf(board.StepInstruction);
        Assert.AreEqual(0xC103, (int)board.Cpu.PC);
        long takenSamePage = board.CyclesOf(board.StepInstruction);

        Assert.AreEqual(2, notTaken);
        Assert.AreEqual(4, takenCrossing);
        Assert.AreEqual(3, takenSamePage);
    }

    #endregion

    #region Interrupts

    [TestMethod]
    public void Irq_ShouldNotBeLostWhileInterruptsAreDisabled()
    {
        var board = NewBoard();
        board.Load(0xC000, 0xEA, 0xEA, 0xEA, 0xEA, 0x58, 0xEA); // NOP x4; CLI; NOP

        // CIA1 Timer A continuous with interrupt enabled
        board.Poke(0xDC0D, 0x7F);
        board.Poke(0xDC04, 0x02, 0x00);
        board.Poke(0xDC0D, 0x81);
        board.Poke(0xDC0E, 0x11);

        for (int i = 0; i < 4; i++) board.StepInstruction(); // with I set the IRQ stays pending
        Assert.IsTrue(board.Cia1.Irq);
        Assert.AreEqual(0xC004, (int)board.Cpu.PC);

        board.StepInstruction(); // CLI
        board.StepInstruction(); // as on the real 6502, one more instruction runs after CLI
        Assert.AreEqual(0xC006, (int)board.Cpu.PC);

        board.StepInstruction(); // IRQ served, then the first instruction of the handler

        ushort irqVector = (ushort)(board.Peek(0xFFFE) | board.Peek(0xFFFF) << 8);
        Assert.IsTrue(board.Cpu.PC > irqVector && board.Cpu.PC <= irqVector + 3, $"PC={board.Cpu.PC:X4}");
        Assert.AreNotEqual(0, board.Cpu.Status & FLAG_I);
    }

    #endregion
}
