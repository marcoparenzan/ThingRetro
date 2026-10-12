using Fake64.Samples;

namespace Fake64.Tests;

// L'assembler 6502 usato per gli esempi
[TestClass]
public sealed class AssemblerTests
{
    static byte[] Bytes(string source) => Assembler.Assemble(source, "test").Bytes;

    [TestMethod]
    public void AddressingModes_ShouldEncodeLikeThe6502()
    {
        var bytes = Bytes("""
            * = $1000
            NOP                 ; implicito
            ASL                 ; accumulatore
            ROL A               ; accumulatore esplicito
            LDA #$12            ; immediato
            LDA $12             ; pagina zero
            LDA $12,X           ; pagina zero,X
            LDX $12,Y           ; pagina zero,Y
            LDA $1234           ; assoluto
            LDA $1234,X         ; assoluto,X
            LDA $1234,Y         ; assoluto,Y
            LDA $12,Y           ; LDA non ha pagina zero,Y: assoluto,Y
            LDA ($12,X)         ; indiretto,X
            LDA ($12),Y         ; indiretto,Y
            JMP ($1234)         ; indiretto
            BNE *               ; relativo: salta a sé stesso
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
                    LDA value           ; costante definita dopo: pagina zero dopo il secondo passaggio
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

        // 10 SYS2061: il codice inizia subito dopo, a $080D
        CollectionAssert.AreEqual(new byte[] { 0x0B, 0x08, 0x0A, 0x00, 0x9E, 0x32, 0x30, 0x36, 0x31, 0x00, 0x00, 0x00 }, result.Bytes[..12]);
        CollectionAssert.AreEqual(new byte[] { 0x41, 0x42, 0x01, 0x02, 0x00, 0xEA, 0xEA, 0x01, 0x43 }, result.Bytes[12..]);
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x08 }, result.Prg[..2]);
    }

    [TestMethod]
    public void OriginMovingForward_ShouldFillWithFF()
    {
        var bytes = Bytes("""
            * = $FFF8
            NOP
            * = $FFFC
            .word $E000
            """);

        CollectionAssert.AreEqual(new byte[] { 0xEA, 0xFF, 0xFF, 0xFF, 0x00, 0xE0 }, bytes);
    }

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
    public void Errors_ShouldNameTheLine()
    {
        var undefined = Assert.ThrowsException<FormatException>(() => Bytes("* = $1000\nNOP\nJMP nowhere"));
        StringAssert.Contains(undefined.Message, "riga 3");
        StringAssert.Contains(undefined.Message, "nowhere");

        var far = Assert.ThrowsException<FormatException>(() => Bytes("* = $1000\nBNE target\n.fill 200, 0\ntarget: RTS"));
        StringAssert.Contains(far.Message, "salto troppo lontano");

        Assert.ThrowsException<FormatException>(() => Bytes("* = $1000\nSTX $1234,X"));  // modo inesistente
        Assert.ThrowsException<FormatException>(() => Bytes("NOP"));                     // manca l'origine
    }
}
