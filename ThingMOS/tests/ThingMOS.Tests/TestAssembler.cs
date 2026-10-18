using ThingMOS.Asm;

namespace ThingMOS.Tests;

// The 6502 assembler used by the samples and the editor
[TestClass]
public sealed class AssemblerTests
{
    static byte[] Bytes(string source) => Assembler.Assemble(source, "test").Bytes;

    [TestMethod]
    public void AddressingModes_ShouldEncodeLikeThe6502()
    {
        var bytes = Bytes("""
            * = $1000
            NOP                 ; implied
            ASL                 ; accumulator
            ROL A               ; explicit accumulator
            LDA #$12            ; immediate
            LDA $12             ; zero page
            LDA $12,X           ; zero page,X
            LDX $12,Y           ; zero page,Y
            LDA $1234           ; absolute
            LDA $1234,X         ; absolute,X
            LDA $1234,Y         ; absolute,Y
            LDA $12,Y           ; LDA has no zero page,Y: absolute,Y
            LDA ($12,X)         ; indirect,X
            LDA ($12),Y         ; indirect,Y
            JMP ($1234)         ; indirect
            BNE *               ; relative: branches to itself
            """);

        CollectionAssert.AreEqual(new byte[]
        {
            0xEA, 0x0A, 0x2A, 0xA9, 0x12, 0xA5, 0x12, 0xB5, 0x12, 0xB6, 0x12,
            0xAD, 0x34, 0x12, 0xBD, 0x34, 0x12, 0xB9, 0x34, 0x12, 0xB9, 0x12, 0x00,
            0xA1, 0x12, 0xB1, 0x12, 0x6C, 0x34, 0x12, 0xD0, 0xFE,
        }, bytes);
    }

    [TestMethod]
    public void Labels_ShouldResolveForwardReferencesAndZeroPage()
    {
        var result = Assembler.Assemble("""
            * = $C000
                    LDA value           ; constant defined later: zero page after the second pass
                    JMP end
            end:    RTS
            value = $FB
            """, "test");

        CollectionAssert.AreEqual(new byte[] { 0xA5, 0xFB, 0x4C, 0x05, 0xC0, 0x60 }, result.Bytes);
        Assert.AreEqual(0xC005, result.Symbols["end"]);
    }

    [TestMethod]
    [DataRow("2+3*4", 14)]
    [DataRow("(2+3)*4", 20)]
    [DataRow("$100/2-1", 0x7F)]
    [DataRow("%1010+'A'", 10 + 0x41)]
    [DataRow("-1+3", 2)]
    public void Expressions_ShouldFollowPrecedence(string expression, int expected)
    {
        var result = Assembler.Assemble($"""
            * = $1000
            X1 = {expression}
            """, "test");

        Assert.AreEqual(expected, result.Symbols["X1"]);
    }

    [TestMethod]
    public void LowHighAndCurrentAddress()
    {
        var bytes = Bytes("""
            * = $1234
            here:   LDA #<here
                    LDX #>here+$100
                    .word *
            """);

        CollectionAssert.AreEqual(new byte[] { 0xA9, 0x34, 0xA2, 0x13, 0x38, 0x12 }, bytes);
    }

    [TestMethod]
    public void Directives_ShouldEmitBasicStubTextAndScreenCodes()
    {
        var result = Assembler.Assemble("""
            * = $0801
            .basic
            .text "AB"
            .screen "AB@"
            .fill 2, $EA
            .byte 1, "C"
            """, "test");

        // 10 SYS2061: the code starts right after it, at $080D
        CollectionAssert.AreEqual(new byte[] { 0x0B, 0x08, 0x0A, 0x00, 0x9E, 0x32, 0x30, 0x36, 0x31, 0x00, 0x00, 0x00 }, result.Bytes[..12]);
        CollectionAssert.AreEqual(new byte[] { 0x41, 0x42, 0x01, 0x02, 0x00, 0xEA, 0xEA, 0x01, 0x43 }, result.Bytes[12..]);
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x08 }, result.Prg[..2]);
    }

    [TestMethod]
    public void Segments_ShouldBeJoinedWithInitmemInTheGaps()
    {
        // As in ACME: the output goes from the lowest to the highest address written, gaps get !initmem (default 0)
        var zeroes = Bytes("""
            * = $FFFC
            .word $E000
            * = $FFF8
            NOP
            """);
        var filled = Bytes("""
            .initmem $FF
            * = $FFF8
            NOP
            * = $FFFC
            .word $E000
            """);

        CollectionAssert.AreEqual(new byte[] { 0xEA, 0x00, 0x00, 0x00, 0x00, 0xE0 }, zeroes);
        CollectionAssert.AreEqual(new byte[] { 0xEA, 0xFF, 0xFF, 0xFF, 0x00, 0xE0 }, filled);
    }

    #region ACME syntax

    [TestMethod]
    public void Acme_LabelsWithoutColonsAndSeveralStatementsPerLine()
    {
        var bytes = Bytes("""
            * = $1000
            start   lda #1 : sta $d020
            .loop   inx : bne .loop
            """);

        CollectionAssert.AreEqual(new byte[] { 0xA9, 0x01, 0x8D, 0x20, 0xD0, 0xE8, 0xD0, 0xFD }, bytes);
    }

    [TestMethod]
    public void Acme_AnonymousAndCheapLocalLabels()
    {
        var bytes = Bytes("""
            * = $1000
            first:
            @loop   dex
                    bne @loop       ; the @loop of "first"
                    beq +           ; the next "+"
            -       iny
            +       bne -           ; the previous "-"
            second:
            @loop   nop             ; a different @loop: a new global label started a new range
                    jmp @loop
            """);

        CollectionAssert.AreEqual(new byte[]
        {
            0xCA, 0xD0, 0xFD,       // $1000 dex, bne $1000
            0xF0, 0x01,             // $1003 beq $1006
            0xC8,                   // $1005 iny
            0xD0, 0xFD,             // $1006 bne $1005
            0xEA, 0x4C, 0x08, 0x10, // $1008 nop, jmp $1008
        }, bytes);
    }

    [TestMethod]
    public void Acme_MacrosWithParametersAndTheirOwnLabels()
    {
        var bytes = Bytes("""
            !macro fill .address, .count {
                ldx #.count
            -   sta .address,x
                dex
                bne -
            }
            * = $1000
            +fill $0400, 3
            +fill $0500, 2
            """);

        CollectionAssert.AreEqual(new byte[]
        {
            0xA2, 0x03, 0x9D, 0x00, 0x04, 0xCA, 0xD0, 0xFA,
            0xA2, 0x02, 0x9D, 0x00, 0x05, 0xCA, 0xD0, 0xFA,
        }, bytes);
    }

    [TestMethod]
    public void Acme_ForLoopsIfAndFloats()
    {
        var bytes = Bytes("""
            * = $1000
            !for i, 0, 3 { !byte i * 2 }
            !for i, 0, 3 {
                !byte int(100 * sin(i * 3.14159265 / 2))
            }
            !if 1 = 2 { !byte $AA } else { !byte $BB }
            !ifdef nothing { !byte 1 }
            """);

        CollectionAssert.AreEqual(new byte[] { 0, 2, 4, 6, 0, 100, 0, 0x9C, 0xBB }, bytes);  // -100 = $9C
    }

    [TestMethod]
    [DataRow("1 + 2 * 3 << 1", 14)]           // shifts come after + and *
    [DataRow("<$1234 + 1", 0x35)]             // < applies to the whole sum
    [DataRow("(5 AND 4) OR 2 XOR 3", 5)]      // OR is the weakest
    [DataRow("2 ^ 3 ^ 2", 512)]               // power, right associative
    [DataRow("7 DIV 2 + 7 MOD 2", 4)]
    [DataRow("-2 ^ 2", -4)]                   // power before negation
    [DataRow("%..##..## + &17 + 0x10", 51 + 15 + 16)]
    [DataRow("1 / 2 * 2", 0)]                 // integer maths...
    [DataRow("int(1.0 / 2 * 2)", 1)]          // ...unless a float is involved
    public void Acme_ExpressionsShouldFollowAcmePriorities(string expression, int expected)
    {
        var result = Assembler.Assemble($"""
            * = $1000
            X1 = {expression}
            """, "test");

        Assert.AreEqual(expected, result.Symbols["X1"]);
    }

    [TestMethod]
    public void Acme_TextConversions()
    {
        var bytes = Bytes("""
            * = $1000
            !scr "aZ@"
            !pet "aZ"
            !raw "aZ"
            !text "a"
            !ct scr { !text "a" }
            """);

        CollectionAssert.AreEqual(new byte[] { 0x01, 0x5A, 0x00, 0x41, 0xDA, 0x61, 0x5A, 0x61, 0x01 }, bytes);
    }

    [TestMethod]
    public void Acme_SourceAndBinaryIncludesAreRelativeToTheFile()
    {
        string folder = Path.Combine(Path.GetTempPath(), "thing65-asm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(folder, "lib"));
        try
        {
            File.WriteAllText(Path.Combine(folder, "lib", "data.asm"), "table: !byte 1, 2\n!bin \"raw.bin\", 2, 1\n");
            File.WriteAllBytes(Path.Combine(folder, "lib", "raw.bin"), [9, 8, 7]);
            File.WriteAllText(Path.Combine(folder, "lib", "broken.asm"), "\n  lda #$100\n");

            var result = Assembler.Assemble("* = $1000\n lda table\n !source \"lib/data.asm\"\n", "main.asm", folder);
            CollectionAssert.AreEqual(new byte[] { 0xAD, 0x03, 0x10, 1, 2, 8, 7 }, result.Bytes);

            var error = Assert.ThrowsException<AssemblerException>(() =>
                Assembler.Assemble("* = $1000\n!src \"lib/broken.asm\"\n", "main.asm", folder));
            Assert.AreEqual("broken.asm", error.SourceName);
            Assert.AreEqual(2, error.Line);
            Assert.AreEqual(Path.Combine(folder, "lib", "broken.asm"), error.FilePath);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    #endregion

    [TestMethod]
    public void Symbols_ShouldBeCaseSensitive()
    {
        var result = Assembler.Assemble("""
            BOTTOM = 150
            * = $1000
            bottom: RTS
            """, "test");

        Assert.AreEqual(150, result.Symbols["BOTTOM"]);
        Assert.AreEqual(0x1000, result.Symbols["bottom"]);
    }

    [TestMethod]
    public void InstructionSet_ShouldListAndDescribeEveryMnemonic()
    {
        // 56 documented mnemonics + 20 undocumented ones; the editor gets these lists for its syntax highlighting
        Assert.AreEqual(76, Assembler.Mnemonics.Count);
        CollectionAssert.IsSubsetOf(new[] { "LDA", "JSR", "LAX", "JAM" }, Assembler.Mnemonics.ToArray());
        foreach (var mnemonic in Assembler.Mnemonics)
            Assert.IsNotNull(InstructionSet.Describe(mnemonic), mnemonic);
        Assert.AreEqual(InstructionSet.Describe("LDA"), InstructionSet.Describe("lda"));
    }

    [TestMethod]
    public void Errors_ShouldNameTheLine()
    {
        var undefined = Assert.ThrowsException<AssemblerException>(() => Bytes("* = $1000\nNOP\nJMP nowhere"));
        StringAssert.Contains(undefined.Message, "line 3");
        StringAssert.Contains(undefined.Message, "nowhere");

        var far = Assert.ThrowsException<AssemblerException>(() => Bytes("* = $1000\nBNE target\n.fill 200, 0\ntarget: RTS"));
        StringAssert.Contains(far.Message, "branch out of range");

        var structured = Assert.ThrowsException<AssemblerException>(() => Bytes("* = $1000\n\n  LDA #$100"));
        Assert.AreEqual(3, structured.Line);
        Assert.AreEqual("value does not fit in a byte: $100", structured.Reason);

        Assert.ThrowsException<AssemblerException>(() => Bytes("* = $1000\nSTX $1234,X"));  // no such mode
        Assert.ThrowsException<AssemblerException>(() => Bytes("NOP"));                     // missing origin
    }
}
