using Thing65.Asm;

namespace Thing65.Tests;

// The assembler with Cpu.Csg4510: the instruction set of the C65's 65CE02 (and MAP/EOM)
[TestClass]
public sealed class Assembler4510Tests
{
    static byte[] Bytes(string source) => Assembler.Assemble("* = $2000\n" + source, "test", cpu: Cpu.Csg4510).Bytes;

    #region The table

    // The assembler's table and the CPU's are two copies of the same opcode map: they must agree
    [TestMethod]
    public void OpcodeTable_ShouldMatchTheCpu()
    {
        for (int opcode = 0; opcode < 256; opcode++)
        {
            var parts = Assembler.OpcodeTable4510[opcode].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var instruction = MOS4510.Instructions[opcode];

            Assert.AreEqual(instruction.Op.ToString(), parts[0], $"mnemonic of ${opcode:X2}");
            var expected = parts[1] switch
            {
                "imp" => MOS4510.Mode.Imp, "acc" => MOS4510.Mode.Acc, "imm" => MOS4510.Mode.Imm, "imm16" => MOS4510.Mode.Imm16,
                "zp" => MOS4510.Mode.Bp, "zpx" => MOS4510.Mode.BpX, "zpy" => MOS4510.Mode.BpY,
                "abs" => MOS4510.Mode.Abs, "abx" => MOS4510.Mode.AbsX, "aby" => MOS4510.Mode.AbsY,
                "izx" => MOS4510.Mode.IndX, "izy" => MOS4510.Mode.IndY, "izz" => MOS4510.Mode.IndZ, "stk" => MOS4510.Mode.StackY,
                "rel" => MOS4510.Mode.Rel, "rel16" => MOS4510.Mode.Rel16, "bpr" => MOS4510.Mode.BpRel,
                "ind" => MOS4510.Mode.AbsInd, "inx" => MOS4510.Mode.AbsXInd,
                _ => throw new InvalidOperationException(parts[1]),
            };
            Assert.AreEqual(expected, instruction.Mode, $"mode of ${opcode:X2} {parts[0]}");
        }
    }

    // Each opcode, as the CPU's disassembler writes it, assembles back to the same bytes: 256 instructions
    [TestMethod]
    public void Disassembly_ShouldAssembleToTheSameBytes()
    {
        for (int opcode = 0; opcode < 256; opcode++)
        {
            var instruction = MOS4510.Instructions[opcode];
            // Operands that cannot be mistaken for a base page address, and relative offsets that stay in range
            (byte low, byte high) = instruction.Mode switch
            {
                MOS4510.Mode.Abs or MOS4510.Mode.AbsX or MOS4510.Mode.AbsY or MOS4510.Mode.AbsInd or MOS4510.Mode.AbsXInd
                    or MOS4510.Mode.Imm16 => ((byte)0x34, (byte)0x12),
                MOS4510.Mode.Rel16 => ((byte)0x34, (byte)0x12),
                MOS4510.Mode.Rel => ((byte)0x10, (byte)0),
                MOS4510.Mode.BpRel => ((byte)0x34, (byte)0x10),
                _ => ((byte)0x34, (byte)0),
            };
            string text = MOS4510.Disassemble((byte)opcode, low, high, 0x2000);

            byte[] bytes;
            try
            {
                bytes = Bytes(text);
            }
            catch (AssemblerException e)
            {
                Assert.Fail($"${opcode:X2} \"{text}\": {e.Message}");
                return;
            }

            Assert.AreEqual(opcode, bytes[0], $"\"{text}\" gives ${bytes[0]:X2}, not ${opcode:X2}");
            int expectedLength = instruction.Mode switch
            {
                MOS4510.Mode.Imp or MOS4510.Mode.Acc => 1,
                MOS4510.Mode.Imm or MOS4510.Mode.Bp or MOS4510.Mode.BpX or MOS4510.Mode.BpY or MOS4510.Mode.IndX or MOS4510.Mode.IndY
                    or MOS4510.Mode.IndZ or MOS4510.Mode.StackY or MOS4510.Mode.Rel => 2,
                _ => 3,
            };
            Assert.AreEqual(expectedLength, bytes.Length, $"length of \"{text}\"");
            if (expectedLength > 1) Assert.AreEqual(low, bytes[1], $"first operand byte of \"{text}\"");
            if (expectedLength > 2) Assert.AreEqual(high, bytes[2], $"second operand byte of \"{text}\"");
        }
    }

    #endregion

    #region Instructions

    [TestMethod]
    public void NewInstructions_ShouldAssemble()
    {
        CollectionAssert.AreEqual(new byte[] { 0xA3, 0x05 }, Bytes("ldz #5"));
        CollectionAssert.AreEqual(new byte[] { 0xB2, 0x10 }, Bytes("lda ($10),z"));
        CollectionAssert.AreEqual(new byte[] { 0xB1, 0x10 }, Bytes("lda ($10),y"));
        CollectionAssert.AreEqual(new byte[] { 0xE2, 0x02 }, Bytes("lda (2,sp),y"));
        CollectionAssert.AreEqual(new byte[] { 0x82, 0x04 }, Bytes("sta (4,SP),Y"));
        CollectionAssert.AreEqual(new byte[] { 0x42 }, Bytes("neg"));
        CollectionAssert.AreEqual(new byte[] { 0x43 }, Bytes("asr"));
        CollectionAssert.AreEqual(new byte[] { 0x44, 0x20 }, Bytes("asr $20"));
        CollectionAssert.AreEqual(new byte[] { 0x1A }, Bytes("inc"));
        CollectionAssert.AreEqual(new byte[] { 0x3A }, Bytes("dec a"));
        CollectionAssert.AreEqual(new byte[] { 0xE3, 0x30 }, Bytes("inw $30"));
        CollectionAssert.AreEqual(new byte[] { 0xCB, 0x00, 0x30 }, Bytes("asw $3000"));
        CollectionAssert.AreEqual(new byte[] { 0xF4, 0x34, 0x12 }, Bytes("phw #$1234"));
        CollectionAssert.AreEqual(new byte[] { 0xFC, 0x00, 0x30 }, Bytes("phw $3000"));
        CollectionAssert.AreEqual(new byte[] { 0x5C }, Bytes("map"));
        CollectionAssert.AreEqual(new byte[] { 0xEA }, Bytes("eom"));
        CollectionAssert.AreEqual(new byte[] { 0x62, 0x03 }, Bytes("rtn #3"));
        CollectionAssert.AreEqual(new byte[] { 0x02, 0x03 }, Bytes("cle\nsee"));
        CollectionAssert.AreEqual(new byte[] { 0x5B, 0x7B, 0x4B, 0x6B, 0x0B, 0x2B }, Bytes("tab\ntba\ntaz\ntza\ntsy\ntys"));
        CollectionAssert.AreEqual(new byte[] { 0x64, 0x10, 0x9C, 0x00, 0x30 }, Bytes("stz $10\nstz $3000"));
        CollectionAssert.AreEqual(new byte[] { 0x22, 0x00, 0x30, 0x23, 0x00, 0x30 }, Bytes("jsr ($3000)\njsr ($3000,x)"));
        CollectionAssert.AreEqual(new byte[] { 0x6C, 0x00, 0x30, 0x7C, 0x00, 0x30 }, Bytes("jmp ($3000)\njmp ($3000,x)"));
    }

    [TestMethod]
    public void BitInstructions_ShouldCarryTheBitInTheOpcode()
    {
        CollectionAssert.AreEqual(new byte[] { 0x07, 0x10, 0x77, 0x11 }, Bytes("rmb0 $10\nrmb7 $11"));
        CollectionAssert.AreEqual(new byte[] { 0x87, 0x10, 0xF7, 0x11 }, Bytes("smb0 $10\nsmb7 $11"));
        // BBR3 $10,target: opcode $3F; the target is relative to the next instruction
        CollectionAssert.AreEqual(new byte[] { 0x3F, 0x10, 0x02 }, Bytes("bbr3 $10,skip\nnop\nnop\nskip: nop").Take(3).ToArray());
        CollectionAssert.AreEqual(new byte[] { 0xBF, 0x10, 0xFD }, Bytes("here: bbs3 $10,here"));
    }

    [TestMethod]
    public void Branches_ShouldBe8BitOrWord()
    {
        CollectionAssert.AreEqual(new byte[] { 0x80, 0x02, 0xEA, 0xEA, 0xEA }, Bytes("bra over\nnop\nnop\nover: nop"));
        // LBRA: relative to the opcode + 2, 16 bits
        CollectionAssert.AreEqual(new byte[] { 0x83, 0xFE, 0xFF }, Bytes("here: lbra here"));
        CollectionAssert.AreEqual(new byte[] { 0x13, 0xFE, 0x00 }, Bytes("lbpl far\n* = $2100\nfar: nop").Take(3).ToArray());
        CollectionAssert.AreEqual(new byte[] { 0x63, 0xFE, 0xFF }, Bytes("here: bsr here"));
    }

    [TestMethod]
    public void WordBranch_ShouldReachFurtherThanTheByteOne()
    {
        string far = "* = $2000\n bne away\n* = $2200\naway: nop\n";
        Assert.ThrowsException<AssemblerException>(() => Assembler.Assemble(far, "t", cpu: Cpu.Csg4510));

        var result = Assembler.Assemble("* = $2000\n lbne away\n* = $2200\naway: nop\n", "t", cpu: Cpu.Csg4510);
        Assert.AreEqual(0xD3, result.Bytes[0]);
        Assert.AreEqual(0x1FE, result.Bytes[1] | result.Bytes[2] << 8); // $2200 - $2002
    }

    #endregion

    #region The processor choice

    [TestMethod]
    public void The6510_ShouldNotKnowTheNewInstructions()
    {
        Assert.ThrowsException<AssemblerException>(() => Assembler.Assemble("* = $2000\n ldz #5\n", "t"));
    }

    [TestMethod]
    public void CpuDirective_ShouldSelectTheInstructionSet()
    {
        var result = Assembler.Assemble("* = $2000\n!cpu 4510\n ldz #5\n!cpu 6510\n lax $10\n", "t");

        CollectionAssert.AreEqual(new byte[] { 0xA3, 0x05, 0xA7, 0x10 }, result.Bytes);
    }

    [TestMethod]
    public void CpuDirective_WithAnUnknownProcessor_ShouldFail()
    {
        Assert.ThrowsException<AssemblerException>(() => Assembler.Assemble("!cpu 65816\n", "t"));
    }

    [TestMethod]
    public void The4510_ShouldKeepTheDocumentedInstructions()
    {
        CollectionAssert.AreEqual(new byte[] { 0xA9, 0x01, 0x8D, 0x20, 0xD0, 0x60 }, Bytes("lda #1\nsta $d020\nrts"));
        CollectionAssert.AreEqual(new byte[] { 0xA5, 0x10, 0xAD, 0x00, 0x30 }, Bytes("lda $10\nlda $3000"));
    }

    [TestMethod]
    public void Mnemonics_ShouldIncludeTheBitVariantsOnThe4510()
    {
        var mnemonics = Assembler.MnemonicsFor(Cpu.Csg4510);

        CollectionAssert.IsSubsetOf(new[] { "LDZ", "MAP", "BBR3", "RMB7", "LBRA", "PHW", "NEG" }, mnemonics.ToArray());
        CollectionAssert.DoesNotContain(mnemonics.ToArray(), "LAX"); // undocumented opcodes of the NMOS 6502
    }

    #endregion
}
