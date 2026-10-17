using static Thing65.Tests.TestHelpers;

namespace Thing65.Tests;

// The 4510 (65CE02 core + memory mapper), on a flat 1 MB RAM. The expected cycles are those of the C65 manual
[TestClass]
public sealed class MOS4510Tests
{
    const int Start = 0x2000;

    sealed class FlatBus : IMappedBus
    {
        public readonly byte[] Memory = new byte[0x100000];
        public readonly List<(int Address, bool Mapped, bool Write)> Log = [];

        public byte Read(int address, bool mapped)
        {
            Log.Add((address, mapped, false));
            return Memory[address];
        }

        public void Write(int address, bool mapped, byte value)
        {
            Log.Add((address, mapped, true));
            Memory[address] = value;
        }
    }

    // CPU after reset with the program at $2000 (the reset vector points there)
    static (MOS4510 Cpu, FlatBus Bus) Setup(string hex, ushort start = Start)
    {
        var bus = new FlatBus();
        Load(bus, start, hex);
        bus.Memory[0xFFFC] = (byte)start;
        bus.Memory[0xFFFD] = (byte)(start >> 8);
        var cpu = new MOS4510 { Bus = bus };
        cpu.Reset();
        return (cpu, bus);
    }

    static void Load(FlatBus bus, int address, string hex)
    {
        foreach (var token in hex.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            bus.Memory[address++] = Convert.ToByte(token, 16);
    }

    // Runs one instruction; returns its cycles
    static int Step(MOS4510 cpu)
    {
        int cycles = 0;
        do { cpu.Tick(); cycles++; } while (!cpu.IsInstructionBoundary);
        return cycles;
    }

    static void Run(MOS4510 cpu, int instructions)
    {
        for (int i = 0; i < instructions; i++) Step(cpu);
    }

    #region Opcode table and cycles

    [TestMethod]
    public void Opcodes_ShouldAllBeDefined()
    {
        Assert.AreEqual(256, MOS4510.Instructions.Length);
        Assert.AreEqual(MOS4510.Op.MAP, MOS4510.Instructions[0x5C].Op); // the former AUG
        Assert.AreEqual(MOS4510.Op.NOP, MOS4510.Instructions[0xEA].Op); // EOM
        Assert.AreEqual(MOS4510.Op.LDZ, MOS4510.Instructions[0xA3].Op);
        Assert.AreEqual(MOS4510.Op.PHW, MOS4510.Instructions[0xF4].Op);
    }

    // Program (hex), cycles according to the manual. Registers are 0, memory is 0, SP = $01FF
    [TestMethod]
    [DataRow("EA", 1, DisplayName = "NOP")]
    [DataRow("E8", 1, DisplayName = "INX")]
    [DataRow("1B", 1, DisplayName = "INZ")]
    [DataRow("0A", 1, DisplayName = "ASL A")]
    [DataRow("42", 2, DisplayName = "NEG")]
    [DataRow("43", 2, DisplayName = "ASR A")]
    [DataRow("02", 2, DisplayName = "CLE")]
    [DataRow("03", 2, DisplayName = "SEE")]
    [DataRow("58", 2, DisplayName = "CLI")]
    [DataRow("78", 2, DisplayName = "SEI")]
    [DataRow("A9 01", 2, DisplayName = "LDA #")]
    [DataRow("A5 10", 3, DisplayName = "LDA bp")]
    [DataRow("B5 10", 3, DisplayName = "LDA bp,X")]
    [DataRow("AD 00 30", 4, DisplayName = "LDA abs")]
    [DataRow("BD FF 30", 4, DisplayName = "LDA abs,X (no page penalty)")]
    [DataRow("A1 10", 5, DisplayName = "LDA (bp,X)")]
    [DataRow("B1 10", 5, DisplayName = "LDA (bp),Y")]
    [DataRow("B2 10", 5, DisplayName = "LDA (bp),Z")]
    [DataRow("E2 02", 6, DisplayName = "LDA (d,SP),Y")]
    [DataRow("85 10", 3, DisplayName = "STA bp")]
    [DataRow("8D 00 30", 4, DisplayName = "STA abs")]
    [DataRow("9D 00 30", 4, DisplayName = "STA abs,X")]
    [DataRow("91 10", 5, DisplayName = "STA (bp),Y")]
    [DataRow("82 02", 6, DisplayName = "STA (d,SP),Y")]
    [DataRow("E6 10", 4, DisplayName = "INC bp")]
    [DataRow("EE 00 30", 5, DisplayName = "INC abs")]
    [DataRow("FE 00 30", 5, DisplayName = "INC abs,X")]
    [DataRow("07 10", 4, DisplayName = "RMB0")]
    [DataRow("87 10", 4, DisplayName = "SMB0")]
    [DataRow("04 10", 4, DisplayName = "TSB bp")]
    [DataRow("1C 00 30", 5, DisplayName = "TRB abs")]
    [DataRow("E3 10", 6, DisplayName = "INW")]
    [DataRow("C3 10", 6, DisplayName = "DEW")]
    [DataRow("CB 00 30", 7, DisplayName = "ASW")]
    [DataRow("0F 10 00", 4, DisplayName = "BBR0")]
    [DataRow("10 00", 2, DisplayName = "BPL")]
    [DataRow("13 00 00", 3, DisplayName = "LBPL")]
    [DataRow("80 05", 2, DisplayName = "BRA")]
    [DataRow("83 00 01", 3, DisplayName = "LBRA")]
    [DataRow("63 00 10", 5, DisplayName = "BSR")]
    [DataRow("20 00 30", 5, DisplayName = "JSR abs")]
    [DataRow("22 00 30", 7, DisplayName = "JSR (abs)")]
    [DataRow("23 00 30", 7, DisplayName = "JSR (abs,X)")]
    [DataRow("4C 00 30", 3, DisplayName = "JMP abs")]
    [DataRow("6C 00 30", 5, DisplayName = "JMP (abs)")]
    [DataRow("7C 00 30", 6, DisplayName = "JMP (abs,X)")]
    [DataRow("60", 4, DisplayName = "RTS")]
    [DataRow("62 00", 7, DisplayName = "RTN")]
    [DataRow("40", 5, DisplayName = "RTI")]
    [DataRow("00", 7, DisplayName = "BRK")]
    [DataRow("48", 3, DisplayName = "PHA")]
    [DataRow("68", 3, DisplayName = "PLA")]
    [DataRow("F4 34 12", 5, DisplayName = "PHW #")]
    [DataRow("FC 00 30", 7, DisplayName = "PHW abs")]
    [DataRow("5C", 4, DisplayName = "MAP")]
    public void Instruction_ShouldTakeTheCyclesOfTheManual(string program, int expected)
    {
        var (cpu, _) = Setup(program);
        Assert.AreEqual(expected, Step(cpu));
    }

    #endregion

    #region Reset, registers and base page

    [TestMethod]
    public void Reset_ShouldSetByteModeStackInPage1()
    {
        var (cpu, _) = Setup("EA");
        cpu.A = cpu.X = cpu.Y = cpu.Z = cpu.B = 0x55;
        cpu.Reset();

        Assert.AreEqual(0, cpu.Z);
        Assert.AreEqual(0, cpu.B);
        Assert.AreEqual(0x01, cpu.SPH);
        Assert.AreNotEqual(0, cpu.Status & 0x20, "E"); // 8-bit stack pointer
        Assert.AreNotEqual(0, cpu.Status & FLAG_I);
        Assert.AreEqual(Start, (int)cpu.PC);
    }

    [TestMethod]
    public void Ldz_Stz_ShouldUseTheZRegister()
    {
        var (cpu, bus) = Setup("A3 12 64 10 9C 00 30"); // LDZ #$12; STZ $10; STZ $3000

        Run(cpu, 3);

        Assert.AreEqual(0x12, bus.Memory[0x10]);
        Assert.AreEqual(0x12, bus.Memory[0x3000]);
    }

    [TestMethod]
    public void IndirectZ_ShouldAddTheZRegisterToThePointer()
    {
        var (cpu, bus) = Setup("A3 02 B2 10"); // LDZ #2; LDA ($10),Z
        bus.Memory[0x10] = 0x00;
        bus.Memory[0x11] = 0x30;
        bus.Memory[0x3002] = 0x99;

        Run(cpu, 2);

        Assert.AreEqual(0x99, cpu.A);
    }

    [TestMethod]
    public void BasePage_ShouldBeRelocatedByTheBRegister()
    {
        var (cpu, bus) = Setup("A9 40 5B A5 05 7B"); // LDA #$40; TAB; LDA $05; TBA
        bus.Memory[0x4005] = 0x77;

        Run(cpu, 3);
        Assert.AreEqual(0x77, cpu.A);
        Assert.AreEqual(0x40, cpu.B);

        Run(cpu, 1); // TBA
        Assert.AreEqual(0x40, cpu.A);
    }

    [TestMethod]
    public void BasePageIndirect_ShouldReadThePointerFromTheRelocatedPage()
    {
        var (cpu, bus) = Setup("A9 50 5B B1 20"); // LDA #$50; TAB; LDA ($20),Y
        bus.Memory[0x5020] = 0x00;
        bus.Memory[0x5021] = 0x31;
        bus.Memory[0x3100] = 0x5A;

        Run(cpu, 3);

        Assert.AreEqual(0x5A, cpu.A);
    }

    #endregion

    #region Stack

    [TestMethod]
    public void Push_InByteMode_ShouldWrapInsideTheStackPage()
    {
        var (cpu, bus) = Setup("A9 AA 48"); // LDA #$AA; PHA
        cpu.SPL = 0x00;

        Run(cpu, 2);

        Assert.AreEqual(0xAA, bus.Memory[0x0100]);
        Assert.AreEqual(0xFF, cpu.SPL);
        Assert.AreEqual(0x01, cpu.SPH);
    }

    [TestMethod]
    public void Push_InWordMode_ShouldUseTheFull16BitPointer()
    {
        // LDY #$30; TYS; LDX #$00; TXS; CLE; LDA #$AA; PHA
        var (cpu, bus) = Setup("A0 30 2B A2 00 9A 02 A9 AA 48");

        Run(cpu, 7);

        Assert.AreEqual(0xAA, bus.Memory[0x3000]);
        Assert.AreEqual(0x2FFF, (int)cpu.SP);
    }

    [TestMethod]
    public void TsyTys_ShouldTransferTheHighByteOfTheStackPointer()
    {
        var (cpu, _) = Setup("0B A0 20 2B"); // TSY; LDY #$20; TYS

        Run(cpu, 1);
        Assert.AreEqual(0x01, cpu.Y);
        Run(cpu, 2);
        Assert.AreEqual(0x20, cpu.SPH);
    }

    [TestMethod]
    public void StackRelativeIndirect_ShouldAddTheOffsetToTheStackPointer()
    {
        var (cpu, bus) = Setup("A0 03 E2 02"); // LDY #3; LDA ($02,SP),Y
        cpu.SP = 0x01F0;
        bus.Memory[0x01F2] = 0x00;
        bus.Memory[0x01F3] = 0x30;
        bus.Memory[0x3003] = 0x6C;

        Run(cpu, 2);

        Assert.AreEqual(0x6C, cpu.A);
    }

    [TestMethod]
    public void PhwImmediate_ShouldPushTheLowByteFirst()
    {
        var (cpu, bus) = Setup("F4 34 12"); // PHW #$1234: the opposite of JSR, so that the first PLA gives the high byte

        Run(cpu, 1);

        Assert.AreEqual(0x34, bus.Memory[0x01FF]);
        Assert.AreEqual(0x12, bus.Memory[0x01FE]);
        Assert.AreEqual(0xFD, cpu.SPL);
    }

    [TestMethod]
    public void PhwAbsolute_ShouldPushTheWordAtTheAddress()
    {
        var (cpu, bus) = Setup("FC 00 30");
        bus.Memory[0x3000] = 0x34;
        bus.Memory[0x3001] = 0x12;

        Run(cpu, 1);

        Assert.AreEqual(0x34, bus.Memory[0x01FF]);
        Assert.AreEqual(0x12, bus.Memory[0x01FE]);
    }

    #endregion

    #region New operations

    [TestMethod]
    public void Neg_ShouldNegateTheAccumulator()
    {
        var (cpu, _) = Setup("A9 05 42");

        Run(cpu, 2);

        Assert.AreEqual(0xFB, cpu.A);
        Assert.AreNotEqual(0, cpu.Status & FLAG_N);
    }

    [TestMethod]
    public void Asr_ShouldKeepTheSignBit()
    {
        var (cpu, _) = Setup("A9 81 43");

        Run(cpu, 2);

        Assert.AreEqual(0xC0, cpu.A);
        Assert.AreNotEqual(0, cpu.Status & FLAG_C);
    }

    [TestMethod]
    public void Asw_ShouldShiftAWordInMemory()
    {
        var (cpu, bus) = Setup("CB 00 30");
        bus.Memory[0x3000] = 0x01;
        bus.Memory[0x3001] = 0x80;

        Run(cpu, 1);

        Assert.AreEqual(0x02, bus.Memory[0x3000]);
        Assert.AreEqual(0x00, bus.Memory[0x3001]);
        Assert.AreNotEqual(0, cpu.Status & FLAG_C);
        Assert.AreEqual(0, cpu.Status & FLAG_N);
    }

    [TestMethod]
    public void Row_ShouldRotateAWordThroughCarry()
    {
        var (cpu, bus) = Setup("38 EB 00 30"); // SEC; ROW $3000
        bus.Memory[0x3000] = 0x00;
        bus.Memory[0x3001] = 0x40;

        Run(cpu, 2);

        Assert.AreEqual(0x01, bus.Memory[0x3000]);
        Assert.AreEqual(0x80, bus.Memory[0x3001]);
        Assert.AreEqual(0, cpu.Status & FLAG_C);
        Assert.AreNotEqual(0, cpu.Status & FLAG_N);
    }

    [TestMethod]
    public void IncrementAndDecrementWord_ShouldCarryBetweenBytes()
    {
        var (cpu, bus) = Setup("E3 10 C3 10 E3 12");
        bus.Memory[0x10] = 0xFF;
        bus.Memory[0x11] = 0x00;
        bus.Memory[0x12] = 0xFF;
        bus.Memory[0x13] = 0xFF;

        Run(cpu, 1);
        Assert.AreEqual(0x00, bus.Memory[0x10]);
        Assert.AreEqual(0x01, bus.Memory[0x11]);

        Run(cpu, 1);
        Assert.AreEqual(0xFF, bus.Memory[0x10]);
        Assert.AreEqual(0x00, bus.Memory[0x11]);

        Run(cpu, 1); // $FFFF + 1
        Assert.AreEqual(0x00, bus.Memory[0x12]);
        Assert.AreEqual(0x00, bus.Memory[0x13]);
        Assert.AreNotEqual(0, cpu.Status & FLAG_Z);
    }

    [TestMethod]
    public void Bit_Immediate_ShouldOnlyTouchZero()
    {
        var (cpu, _) = Setup("A9 0F 89 F0"); // LDA #$0F; BIT #$F0

        Run(cpu, 2);

        Assert.AreNotEqual(0, cpu.Status & FLAG_Z);
        Assert.AreEqual(0, cpu.Status & (FLAG_N | FLAG_V));
    }

    [TestMethod]
    public void TsbTrb_ShouldSetAndResetBits()
    {
        var (cpu, bus) = Setup("A9 0F 04 10 A9 03 14 11"); // LDA #$0F; TSB $10; LDA #$03; TRB $11
        bus.Memory[0x10] = 0xF0;
        bus.Memory[0x11] = 0xFF;

        Run(cpu, 2);
        Assert.AreEqual(0xFF, bus.Memory[0x10]);
        Assert.AreNotEqual(0, cpu.Status & FLAG_Z); // A & M was 0

        Run(cpu, 2);
        Assert.AreEqual(0xFC, bus.Memory[0x11]);
        Assert.AreEqual(0, cpu.Status & FLAG_Z);
    }

    [TestMethod]
    public void RmbSmb_ShouldClearAndSetABit()
    {
        var (cpu, bus) = Setup("C7 10 27 11"); // SMB4 $10; RMB2 $11
        bus.Memory[0x10] = 0x00;
        bus.Memory[0x11] = 0xFF;

        Run(cpu, 2);

        Assert.AreEqual(0x10, bus.Memory[0x10]);
        Assert.AreEqual(0xFB, bus.Memory[0x11]);
    }

    [TestMethod]
    [DataRow("BF 10 02", 0x08, 0x2005, DisplayName = "BBS3 with the bit set jumps")]
    [DataRow("BF 10 02", 0x00, 0x2003, DisplayName = "BBS3 with the bit clear falls through")]
    [DataRow("3F 10 02", 0x00, 0x2005, DisplayName = "BBR3 with the bit clear jumps")]
    [DataRow("3F 10 02", 0x08, 0x2003, DisplayName = "BBR3 with the bit set falls through")]
    public void BranchOnBit_ShouldTestABasePageBit(string program, int value, int expectedPc)
    {
        var (cpu, bus) = Setup(program);
        bus.Memory[0x10] = (byte)value;

        Run(cpu, 1);

        Assert.AreEqual(expectedPc, (int)cpu.PC);
    }

    #endregion

    #region Branches and subroutines

    [TestMethod]
    public void Bra_ShouldBeRelativeToOpcodePlus2()
    {
        var (cpu, _) = Setup("80 05");
        Run(cpu, 1);
        Assert.AreEqual(0x2007, (int)cpu.PC);
    }

    [TestMethod]
    public void Lbra_ShouldBeRelativeToOpcodePlus2()
    {
        var (cpu, _) = Setup("83 FE 0F"); // $2002 + $0FFE = $3000
        Run(cpu, 1);
        Assert.AreEqual(0x3000, (int)cpu.PC);
    }

    [TestMethod]
    public void WordBranch_Backwards_ShouldWork()
    {
        var (cpu, _) = Setup("83 FC FF"); // $2002 - 4
        Run(cpu, 1);
        Assert.AreEqual(0x1FFE, (int)cpu.PC);
    }

    [TestMethod]
    public void ConditionalWordBranch_ShouldFollowTheFlag()
    {
        var (cpu, _) = Setup("F3 00 01 D3 00 01"); // LBEQ +$100; LBNE +$100 (Z is clear at reset)

        Run(cpu, 1); // not taken
        Assert.AreEqual(0x2003, (int)cpu.PC);

        Run(cpu, 1); // taken: $2005 + $100
        Assert.AreEqual(0x2105, (int)cpu.PC);
    }

    [TestMethod]
    public void BsrAndRts_ShouldReturnAfterTheInstruction()
    {
        var (cpu, bus) = Setup("63 FE 0F"); // BSR $3000
        bus.Memory[0x3000] = 0x60;          // RTS

        Run(cpu, 1);
        Assert.AreEqual(0x3000, (int)cpu.PC);
        Assert.AreEqual(0x20, bus.Memory[0x01FF]); // the pushed address points to the last byte (opcode + 2)
        Assert.AreEqual(0x02, bus.Memory[0x01FE]);
        Assert.AreEqual(0xFD, cpu.SPL);

        Run(cpu, 1);
        Assert.AreEqual(0x2003, (int)cpu.PC);
        Assert.AreEqual(0xFF, cpu.SPL);
    }

    [TestMethod]
    public void JsrIndirect_ShouldJumpThroughAPointer()
    {
        var (cpu, bus) = Setup("22 00 30 23 10 30 A2 04"); // JSR ($3000); JSR ($3010,X) with X = 4 later
        bus.Memory[0x3000] = 0x00;
        bus.Memory[0x3001] = 0x40;
        bus.Memory[0x4000] = 0x60; // RTS

        Run(cpu, 1);
        Assert.AreEqual(0x4000, (int)cpu.PC);
        Run(cpu, 1); // RTS
        Assert.AreEqual(0x2003, (int)cpu.PC);

        cpu.X = 4;
        bus.Memory[0x3014] = 0x00;
        bus.Memory[0x3015] = 0x50;
        bus.Memory[0x5000] = 0x60;
        Run(cpu, 1);
        Assert.AreEqual(0x5000, (int)cpu.PC);
    }

    [TestMethod]
    public void JmpIndirect_ShouldNotWrapInThePage()
    {
        var (cpu, bus) = Setup("6C FF 30"); // JMP ($30FF): the high byte is at $3100
        bus.Memory[0x30FF] = 0x34;
        bus.Memory[0x3100] = 0x12;

        Run(cpu, 1);

        Assert.AreEqual(0x1234, (int)cpu.PC);
    }

    [TestMethod]
    public void JmpIndexedIndirect_ShouldAddX()
    {
        var (cpu, bus) = Setup("A2 02 7C 00 30");
        bus.Memory[0x3002] = 0x78;
        bus.Memory[0x3003] = 0x56;

        Run(cpu, 2);

        Assert.AreEqual(0x5678, (int)cpu.PC);
    }

    [TestMethod]
    public void Rtn_ShouldDropTheArgumentsFromTheStack()
    {
        var (cpu, bus) = Setup("62 03"); // RTN #3
        cpu.SPL = 0xF0;
        bus.Memory[0x01F1] = 0x33;
        bus.Memory[0x01F2] = 0x44;

        Run(cpu, 1);

        Assert.AreEqual(0x4434, (int)cpu.PC);
        Assert.AreEqual(0xF2 + 3, (int)cpu.SPL);
    }

    #endregion

    #region Interrupts

    [TestMethod]
    public void Brk_ShouldPushPcPlus2AndStatusWithB()
    {
        var (cpu, bus) = Setup("00");
        bus.Memory[0xFFFE] = 0x00;
        bus.Memory[0xFFFF] = 0x40;
        cpu.Status = (byte)(cpu.Status & ~FLAG_I | FLAG_D);

        Run(cpu, 1);

        Assert.AreEqual(0x4000, (int)cpu.PC);
        Assert.AreEqual(0x20, bus.Memory[0x01FF]);
        Assert.AreEqual(0x02, bus.Memory[0x01FE]);
        Assert.AreNotEqual(0, bus.Memory[0x01FD] & FLAG_B);
        Assert.AreNotEqual(0, cpu.Status & FLAG_I);
        Assert.AreEqual(0, cpu.Status & FLAG_D, "interrupts clear the decimal mode");
    }

    [TestMethod]
    public void Irq_ShouldPushPcAndStatusWithoutB()
    {
        var (cpu, bus) = Setup("A9 01 EA");
        bus.Memory[0xFFFE] = 0x00;
        bus.Memory[0xFFFF] = 0x40;
        cpu.Status &= unchecked((byte)~FLAG_I);
        cpu.IrqInput = () => true;

        Step(cpu);                      // LDA #1 (two cycles): the interrupt is polled at its end
        int cycles = Step(cpu);         // the interrupt sequence replaces the next instruction

        Assert.AreEqual(7, cycles);
        Assert.AreEqual(0x4000, (int)cpu.PC);
        Assert.AreEqual(0x20, bus.Memory[0x01FF]);
        Assert.AreEqual(0x02, bus.Memory[0x01FE]);
        Assert.AreEqual(0, bus.Memory[0x01FD] & FLAG_B);
    }

    [TestMethod]
    public void Irq_ShouldNotOccurAfterAOneCycleInstruction()
    {
        var (cpu, bus) = Setup("E8 A9 01 EA");
        bus.Memory[0xFFFE] = 0x00;
        bus.Memory[0xFFFF] = 0x40;
        cpu.Status &= unchecked((byte)~FLAG_I);
        cpu.IrqInput = () => true;

        Step(cpu); // INX: one cycle
        Step(cpu); // not interrupted: LDA #1 runs
        Assert.AreEqual(1, cpu.A);

        Step(cpu); // the interrupt sequence, after the two-cycle LDA
        Assert.AreEqual(0x4000, (int)cpu.PC);
    }

    [TestMethod]
    public void Cli_TakesTwoCyclesSoAnIrqCanOccurAtOnce()
    {
        var (cpu, bus) = Setup("58 E8");
        bus.Memory[0xFFFE] = 0x00;
        bus.Memory[0xFFFF] = 0x40;
        cpu.IrqInput = () => true;

        Step(cpu); // CLI
        Step(cpu); // interrupt, not INX

        Assert.AreEqual(0x4000, (int)cpu.PC);
        Assert.AreEqual(0, cpu.X);
    }

    [TestMethod]
    public void Nmi_ShouldTriggerOnTheFallingEdge()
    {
        var (cpu, bus) = Setup("A9 01 A9 02");
        bus.Memory[0xFFFA] = 0x00;
        bus.Memory[0xFFFB] = 0x50;
        bool nmi = false;
        cpu.NmiInput = () => nmi;

        Step(cpu);
        nmi = true;
        Step(cpu); // LDA #2 ends with the NMI pending... the sequence is next
        Step(cpu);

        Assert.AreEqual(0x5000, (int)cpu.PC);
    }

    [TestMethod]
    public void Rti_ShouldRestoreStatusAndPc()
    {
        var (cpu, bus) = Setup("40");
        cpu.SPL = 0xFC;
        bus.Memory[0x01FD] = FLAG_C | FLAG_Z;
        bus.Memory[0x01FE] = 0x34;
        bus.Memory[0x01FF] = 0x12;

        Run(cpu, 1);

        Assert.AreEqual(0x1234, (int)cpu.PC);
        Assert.AreEqual(FLAG_C | FLAG_Z, cpu.Status & (FLAG_C | FLAG_Z));
    }

    [TestMethod]
    public void Plp_ShouldNotChangeTheEFlag()
    {
        var (cpu, bus) = Setup("28");
        cpu.SPL = 0xFE;
        bus.Memory[0x01FF] = 0x00; // E clear in the pulled byte

        Run(cpu, 1);

        Assert.AreNotEqual(0, cpu.Status & 0x20, "E stays set");
    }

    #endregion

    #region Memory mapper

    // LDA #$10; LDX #$23; LDY #$00; LDZ #$00; MAP; EOM; LDA $2000. The program is in block 0, which stays unmapped
    const string MapProgram = "A9 10 A2 23 A0 00 A3 00 5C EA AD 00 20";

    [TestMethod]
    public void Map_ShouldAddTheOffsetToTheMappedBlocks()
    {
        var (cpu, bus) = Setup(MapProgram, 0x1000);
        bus.Memory[0x2000] = 0x11;   // unmapped
        bus.Memory[0x33000] = 0x22;  // $2000 + $31000

        Run(cpu, 5); // up to MAP
        bus.Log.Clear();
        Run(cpu, 2); // EOM; LDA $2000

        Assert.AreEqual(0x22, cpu.A);
        Assert.AreEqual(0x31000, cpu.LowerOffset);
        Assert.AreEqual(0x02, cpu.MapMask);
        CollectionAssert.Contains(bus.Log, (0x33000, true, false));
    }

    [TestMethod]
    public void Map_BlocksNotInTheMask_ShouldStayUnmapped()
    {
        var (cpu, _) = Setup(MapProgram, 0x1000);

        Run(cpu, 5);
        var (address, mapped) = cpu.Translate(0x1000);
        Assert.AreEqual(0x1000, address);
        Assert.IsFalse(mapped);
        (address, mapped) = cpu.Translate(0x2000);
        Assert.AreEqual(0x33000, address);
        Assert.IsTrue(mapped);
    }

    [TestMethod]
    public void Map_UpperRegion_ShouldUseItsOwnOffset()
    {
        // A=0, X=0, Y=$20, Z=$53: blocks 4 and 6 are mapped, with offset $3_2000
        var (cpu, _) = Setup("A9 00 A2 00 A0 20 A3 53 5C EA", 0x1000);

        Run(cpu, 5);

        Assert.AreEqual(0x32000, cpu.UpperOffset);
        Assert.AreEqual(0x50, (int)cpu.MapMask);
        var (address, mapped) = cpu.Translate(0x8000); // block 4
        Assert.AreEqual(0x3A000, address);
        Assert.IsTrue(mapped);
        (_, mapped) = cpu.Translate(0xA000); // block 5 is not in the mask
        Assert.IsFalse(mapped);
    }

    [TestMethod]
    public void Map_ShouldInhibitInterruptsUntilEom()
    {
        var (cpu, bus) = Setup("5C E8 EA E8", 0x1000);
        bus.Memory[0xFFFE] = 0x00;
        bus.Memory[0xFFFF] = 0x40;
        cpu.Status &= unchecked((byte)~FLAG_I);
        cpu.IrqInput = () => true;

        Step(cpu); // MAP
        Step(cpu); // INX: no interrupt yet (and it is a one-cycle instruction anyway)
        Assert.AreEqual(1, cpu.X);
        Assert.AreEqual(0x1002, (int)cpu.PC);
        Step(cpu); // EOM
        Step(cpu); // INX
        Assert.AreEqual(2, cpu.X);
    }

    [TestMethod]
    public void Reset_ShouldClearTheMapper()
    {
        var (cpu, _) = Setup(MapProgram, 0x1000);
        Run(cpu, 5);
        Assert.AreNotEqual(0, cpu.MapMask);

        cpu.Reset();

        Assert.AreEqual(0, cpu.MapMask);
        var (_, mapped) = cpu.Translate(0x2000);
        Assert.IsFalse(mapped);
    }

    #endregion

    #region Decimal mode

    [TestMethod]
    [DataRow(0x09, 0x01, false, 0x10, false)]
    [DataRow(0x99, 0x01, false, 0x00, true)]
    [DataRow(0x58, 0x46, false, 0x04, true)]
    [DataRow(0x12, 0x34, true, 0x47, false)]
    public void Adc_DecimalMode_ShouldAddBcd(int a, int operand, bool carryIn, int expected, bool carryOut)
    {
        var (cpu, _) = Setup($"F8 {(carryIn ? "38" : "18")} A9 {a:X2} 69 {operand:X2}");

        Run(cpu, 4);

        Assert.AreEqual(expected, cpu.A);
        Assert.AreEqual(carryOut, (cpu.Status & FLAG_C) != 0);
        Assert.AreEqual(expected == 0, (cpu.Status & FLAG_Z) != 0, "on CMOS Z describes the BCD result");
    }

    [TestMethod]
    [DataRow(0x50, 0x01, true, 0x49, true)]
    [DataRow(0x00, 0x01, true, 0x99, false)]
    [DataRow(0x46, 0x12, true, 0x34, true)]
    [DataRow(0x40, 0x13, false, 0x26, true)]
    public void Sbc_DecimalMode_ShouldSubtractBcd(int a, int operand, bool carryIn, int expected, bool carryOut)
    {
        var (cpu, _) = Setup($"F8 {(carryIn ? "38" : "18")} A9 {a:X2} E9 {operand:X2}");

        Run(cpu, 4);

        Assert.AreEqual(expected, cpu.A);
        Assert.AreEqual(carryOut, (cpu.Status & FLAG_C) != 0);
    }

    #endregion

    #region Klaus Dormann, 65C02 extended opcodes test

    // The CMOS instructions that the 65CE02 shares with the Rockwell 65C02: (zp) is (bp),Z with Z = 0, BBR/BBS,
    // RMB/SMB, TSB/TRB, STZ, BRA, INC/DEC A, PHX/PHY..., JMP (abs,X), BIT #imm, the decimal mode flags.
    // The program starts at $0400 and ends in "JMP *": at $24F1 if all the tests passed.
    // The test of the 65C02's NOPs ($0C04-$1653: $02, $22, $42... which are CLE, JSR (abs), NEG... on the 65CE02,
    // all 256 opcodes being defined) makes no sense here and is skipped
    [TestMethod]
    [Timeout(120_000)]
    public void KlausDormann_65C02ExtendedOpcodesTest_ShouldReachSuccessTrap()
    {
        string image = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestData", "External", "KlausDormann",
            "65C02_extended_opcodes_test.bin");
        if (!File.Exists(image)) Assert.Inconclusive("Test missing: run tools/Download-CpuTests.ps1 -SkipSingleStep");

        var bus = new FlatBus();
        File.ReadAllBytes(image).CopyTo(bus.Memory, 0);
        bus.Memory[0xFFFC] = 0x00;
        bus.Memory[0xFFFD] = 0x04;
        var cpu = new MOS4510 { Bus = bus };
        cpu.Reset();

        ushort previous = 0;
        long instructions = 0;
        while (true)
        {
            if (cpu.PC == 0x0C04) cpu.PC = 0x1654; // skip the NOP tests, to the next test
            Step(cpu);
            instructions++;
            if (cpu.PC == previous) break; // JMP * : end of the test
            previous = cpu.PC;
            if (instructions > 100_000_000) Assert.Fail($"The test does not end (PC=${cpu.PC:X4})");
        }

        Assert.AreEqual(0x24F1, (int)cpu.PC, $"Error trap at ${cpu.PC:X4}: see 65C02_extended_opcodes_test.lst");
    }

    #endregion
}
