using System.Text;

namespace Fake64.Samples;

// Un esempio: il file da caricare nell'emulatore e, per i programmi BASIC, il sorgente leggibile
public sealed record Sample(string FileName, string Description, byte[] Content, string? BasicSource = null);

// I programmi di esempio di samples/, ciascuno dedicato a una funzione dell'emulatore
public static class SampleSet
{
    public static IReadOnlyList<Sample> All { get; } =
    [
        Hello(),
        RasterBars(),
        Sprite(),
        Keys(),
        Bitmap(),
        HelloCartridge(),
    ];

    static Sample Basic(string fileName, string description, string source) =>
        new(fileName, description, BasicTokenizer.Tokenize(source), source.Trim() + "\n");

    // Testo e colori: POKE 646 è il colore del cursore usato da PRINT (il 6, blu come lo sfondo, si salta)
    static Sample Hello() => Basic("hello.prg", "BASIC e colori del testo", """
        10 PRINT CHR$(147);
        20 FOR I=0 TO 15
        30 IF I<>6 THEN POKE 646,I:PRINT TAB(I);"HELLO, FAKE64!"
        40 NEXT
        50 POKE 646,14
        """);

    // Raster interrupt: a riga 100 il bordo diventa rosso, a riga 150 torna azzurro.
    // Il gestore condivide il vettore $0314 con l'IRQ della CIA 1, che continua a passare al KERNAL:
    // tastiera e BASIC restano utilizzabili
    static Sample RasterBars() => new("rasterbars.prg", "Raster interrupt e split del bordo", BasicTokenizer.SysStub(
    [
        0x78,                               // 080D        SEI
        0xA9, 0x01, 0x8D, 0x1A, 0xD0,       // 080E        LDA #$01 : STA $D01A    raster interrupt abilitato
        0xA9, 0x64, 0x8D, 0x12, 0xD0,       // 0813        LDA #100 : STA $D012    riga 100
        0xAD, 0x11, 0xD0, 0x29, 0x7F,       // 0818        LDA $D011 : AND #$7F    bit 8 del confronto a 0
        0x8D, 0x11, 0xD0,                   // 081D        STA $D011
        0xA9, 0x2C, 0x8D, 0x14, 0x03,       // 0820        LDA #<irq : STA $0314
        0xA9, 0x08, 0x8D, 0x15, 0x03,       // 0825        LDA #>irq : STA $0315
        0x58,                               // 082A        CLI
        0x60,                               // 082B        RTS                     torna al BASIC
        0xAD, 0x19, 0xD0,                   // 082C irq:   LDA $D019
        0x30, 0x03,                         // 082F        BMI raster              interrupt del VIC-II?
        0x4C, 0x31, 0xEA,                   // 0831        JMP $EA31               no: IRQ della CIA 1, al KERNAL
        0x8D, 0x19, 0xD0,                   // 0834 raster:STA $D019               conferma
        0xAD, 0x12, 0xD0,                   // 0837        LDA $D012
        0xC9, 0x78,                         // 083A        CMP #120
        0xB0, 0x0D,                         // 083C        BCS bottom
        0xA9, 0x02, 0x8D, 0x20, 0xD0,       // 083E        LDA #2 : STA $D020      bordo rosso
        0xA9, 0x96, 0x8D, 0x12, 0xD0,       // 0843        LDA #150 : STA $D012    prossimo interrupt a riga 150
        0x4C, 0x81, 0xEA,                   // 0848        JMP $EA81               ripristina i registri, RTI
        0xA9, 0x0E, 0x8D, 0x20, 0xD0,       // 084B bottom:LDA #14 : STA $D020     bordo azzurro
        0xA9, 0x64, 0x8D, 0x12, 0xD0,       // 0850        LDA #100 : STA $D012
        0x4C, 0x81, 0xEA,                   // 0855        JMP $EA81
    ]));

    // Uno sprite espanso che rimbalza; la forma (24 x 21 pixel) è disegnata qui sotto e diventa DATA
    static Sample Sprite()
    {
        string[] shape =
        [
            "........XXXXXXXX........",
            "......XXXXXXXXXXXX......",
            ".....XXXXXXXXXXXXXX.....",
            "....XXXXXXXXXXXXXXXX....",
            "...XXXX..XXXXXX..XXXX...",
            "...XXXX..XXXXXX..XXXX...",
            "..XXXXXXXXXXXXXXXXXXXX..",
            "..XXXXXXXXXXXXXXXXXXXX..",
            "..XXXXXXXXXXXXXXXXXXXX..",
            "..XXXXXXXXXXXXXXXXXXXX..",
            "..XXX.XXXXXXXXXXXX.XXX..",
            "...XXX.XXXXXXXXXX.XXX...",
            "...XXXX.XXXXXXXX.XXXX...",
            "....XXXX........XXXX....",
            ".....XXXXXXXXXXXXXX.....",
            "......XXXXXXXXXXXX......",
            "........XXXXXXXX........",
            "........................",
            "........................",
            "........................",
            "........................",
        ];
        var data = new StringBuilder();
        int line = 100;
        foreach (var row in shape)
        {
            var bytes = Enumerable.Range(0, 3).Select(b => Convert.ToByte(row.Substring(b * 8, 8).Replace('X', '1').Replace('.', '0'), 2));
            data.AppendLine($"{line} DATA {string.Join(",", bytes)}");
            line += 10;
        }

        return Basic("sprite.prg", "Uno sprite espanso che rimbalza", $"""
            10 V=53248:PRINT CHR$(147)
            20 FOR I=0 TO 62:READ D:POKE 832+I,D:NEXT
            30 POKE 2040,13:POKE V+39,7:POKE V+23,1:POKE V+29,1:POKE V+21,1
            40 X=24:Y=50:DX=2:DY=1
            50 POKE V,X AND 255:POKE V+16,-(X>255):POKE V+1,Y
            60 X=X+DX:Y=Y+DY
            70 IF X<24 OR X>296 THEN DX=-DX
            80 IF Y<50 OR Y>208 THEN DY=-DY
            90 GOTO 50
            {data}
            """);
    }

    // Tasti funzione e cursore: F1-F8 arrivano come codici 133-140, i tasti cursore come 17, 145, 29, 157
    static Sample Keys() => Basic("keys.prg", "Tasti funzione e tasti cursore", """
        10 PRINT CHR$(147)"PREMI F1-F8 O I TASTI CURSORE"
        20 PRINT "(Q PER USCIRE)":PRINT
        30 GET K$:IF K$="" THEN 30
        40 IF K$="Q" THEN END
        50 K=ASC(K$)
        60 IF K>=133 AND K<=140 THEN PRINT "F";MID$("13572468",K-132,1):GOTO 30
        70 IF K=17 THEN PRINT "CURSORE GIU'":GOTO 30
        80 IF K=145 THEN PRINT "CURSORE SU":GOTO 30
        90 IF K=29 THEN PRINT "CURSORE DESTRA":GOTO 30
        100 IF K=157 THEN PRINT "CURSORE SINISTRA":GOTO 30
        110 PRINT "CODICE";K:GOTO 30
        """);

    // Modo bitmap hires a $2000. La bitmap è fatta di celle di 8x8 pixel (8 byte consecutivi): ogni cella
    // contiene una diagonale, con il verso che cambia da una colonna di celle alla successiva (zig-zag)
    static Sample Bitmap() => new("bitmap.prg", "Modo bitmap hires in linguaggio macchina", BasicTokenizer.SysStub(
    [
        0xA9, 0x3B, 0x8D, 0x11, 0xD0,       // 080D        LDA #$3B : STA $D011    bitmap, display attivo, 25 righe
        0xA9, 0x18, 0x8D, 0x18, 0xD0,       // 0812        LDA #$18 : STA $D018    bitmap a $2000, colori a $0400
        0xA2, 0x00,                         // 0817        LDX #0
        0xA9, 0xE6,                         // 0819        LDA #$E6                pixel accesi azzurri, spenti blu
        0x9D, 0x00, 0x04,                   // 081B color: STA $0400,X
        0x9D, 0x00, 0x05,                   // 081E        STA $0500,X
        0x9D, 0x00, 0x06,                   // 0821        STA $0600,X
        0x9D, 0x00, 0x07,                   // 0824        STA $0700,X
        0xE8,                               // 0827        INX
        0xD0, 0xF1,                         // 0828        BNE color
        0xA9, 0x00, 0x85, 0xFB,             // 082A        LDA #<$2000 : STA $FB
        0xA9, 0x20, 0x85, 0xFC,             // 082E        LDA #>$2000 : STA $FC
        0xA0, 0x00,                         // 0832        LDY #0
        0x98,                               // 0834 fill:  TYA
        0x29, 0x07,                         // 0835        AND #$07                riga nella cella
        0x85, 0xFD,                         // 0837        STA $FD
        0x98,                               // 0839        TYA
        0x29, 0x08,                         // 083A        AND #$08                colonna di celle pari o dispari
        0x05, 0xFD,                         // 083C        ORA $FD
        0xAA,                               // 083E        TAX
        0xBD, 0x52, 0x08,                   // 083F        LDA table,X
        0x91, 0xFB,                         // 0842        STA ($FB),Y
        0xC8,                               // 0844        INY
        0xD0, 0xED,                         // 0845        BNE fill
        0xE6, 0xFC,                         // 0847        INC $FC
        0xA5, 0xFC,                         // 0849        LDA $FC
        0xC9, 0x40,                         // 084B        CMP #$40                fino a $3FFF
        0xD0, 0xE5,                         // 084D        BNE fill
        0x4C, 0x4F, 0x08,                   // 084F done:  JMP done                resta in bitmap
        0x80, 0x40, 0x20, 0x10, 0x08, 0x04, 0x02, 0x01, // 0852 table: diagonale che scende verso destra
        0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80, // 085A        diagonale che sale verso destra
    ]));

    // Cartuccia 8K con avvio automatico: il KERNAL trova la firma "CBM80" a $8004 e salta al vettore a $8000.
    // Il codice inizializza il sistema come farebbe il KERNAL e scrive un messaggio
    static Sample HelloCartridge()
    {
        byte[] code =
        [
            0x09, 0x80, 0x09, 0x80,             // 8000        vettori di avvio a freddo e a caldo: $8009
            0xC3, 0xC2, 0xCD, 0x38, 0x30,       // 8004        "CBM80"
            0x20, 0x84, 0xFF,                   // 8009        JSR IOINIT
            0x20, 0x87, 0xFF,                   // 800C        JSR RAMTAS
            0x20, 0x8A, 0xFF,                   // 800F        JSR RESTOR
            0x20, 0x81, 0xFF,                   // 8012        JSR CINT
            0xA2, 0x00,                         // 8015        LDX #0
            0xBD, 0x25, 0x80,                   // 8017 print: LDA message,X
            0xF0, 0x06,                         // 801A        BEQ done
            0x20, 0xD2, 0xFF,                   // 801C        JSR CHROUT
            0xE8,                               // 801F        INX
            0xD0, 0xF5,                         // 8020        BNE print
            0x4C, 0x22, 0x80,                   // 8022 done:  JMP done
            .. Encoding.ASCII.GetBytes("HELLO FROM CARTRIDGE"), 0x00, // 8025 message
        ];
        return new("hello.crt", "Cartuccia 8K con avvio automatico",
            CrtImage.Build("HELLO CART", CrtImage.TypeNormal, exromActive: true, gameActive: false, (0, 0x8000, CrtImage.Rom8K(code))));
    }
}
