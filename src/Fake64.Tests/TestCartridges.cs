using System.Buffers.Binary;
using System.Text;
using Fake64.Crt;
using static Fake64.Tests.TestHelpers;

namespace Fake64.Tests;

[TestClass]
public sealed class CartridgeTests
{
    const long CYCLES_PER_SECOND = 985248;

    record ChipData(int Bank, int LoadAddress, byte[] Data);

    // Costruisce un file .crt in memoria; exrom/game = livello della linea (true = alta, inattiva)
    static byte[] Crt(int type, bool exrom, bool game, params ChipData[] chips)
    {
        var crt = new List<byte>();
        var header = new byte[0x40];
        Encoding.ASCII.GetBytes("C64 CARTRIDGE   ").CopyTo(header, 0);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0x10), 0x40);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(0x14), 0x0100);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(0x16), (ushort)type);
        header[0x18] = (byte)(exrom ? 1 : 0);
        header[0x19] = (byte)(game ? 1 : 0);
        Encoding.ASCII.GetBytes("TEST CART").CopyTo(header, 0x20);
        crt.AddRange(header);

        foreach (var chip in chips)
        {
            var chipHeader = new byte[0x10];
            Encoding.ASCII.GetBytes("CHIP").CopyTo(chipHeader, 0);
            BinaryPrimitives.WriteUInt32BigEndian(chipHeader.AsSpan(4), (uint)(0x10 + chip.Data.Length));
            BinaryPrimitives.WriteUInt16BigEndian(chipHeader.AsSpan(10), (ushort)chip.Bank);
            BinaryPrimitives.WriteUInt16BigEndian(chipHeader.AsSpan(12), (ushort)chip.LoadAddress);
            BinaryPrimitives.WriteUInt16BigEndian(chipHeader.AsSpan(14), (ushort)chip.Data.Length);
            crt.AddRange(chipHeader);
            crt.AddRange(chip.Data);
        }
        return crt.ToArray();
    }

    // ROM da 8 KB riempita con un valore, con eventuali byte iniziali specifici
    static byte[] Rom(byte fill, params byte[] start)
    {
        var rom = new byte[0x2000];
        Array.Fill(rom, fill);
        start.CopyTo(rom, 0);
        return rom;
    }

    // ROML con autostart: vettori a $8009, firma "CBM80", poi LDA #color : STA $D020 : JMP *
    static byte[] AutostartRoml(byte color) => Rom(0xEA,
        0x09, 0x80, 0x09, 0x80, 0xC3, 0xC2, 0xCD, 0x38, 0x30,
        0xA9, color, 0x8D, 0x20, 0xD0, 0x4C, 0x0E, 0x80);

    static Board WithCartridge(byte[] crt)
    {
        var board = NewBoard();
        board.InsertCartridge(CrtFile.Load(crt));
        return board;
    }

    #region Formato .crt

    [TestMethod]
    public void Load_ShouldReadNameTypeAndLines()
    {
        var cartridge = CrtFile.Load(Crt(CrtFile.TYPE_NORMAL, exrom: false, game: false,
            new ChipData(0, 0x8000, new byte[0x4000])));

        Assert.AreEqual("TEST CART", cartridge.Name);
        Assert.AreEqual("Normal 16K", cartridge.TypeName);
        Assert.IsFalse(cartridge.Exrom);
        Assert.IsFalse(cartridge.Game);
    }

    [TestMethod]
    public void Load_ShouldRejectInvalidFiles()
    {
        Assert.ThrowsException<InvalidDataException>(() => CrtFile.Load(new byte[100]));
        Assert.ThrowsException<InvalidDataException>(() => CrtFile.Load(Crt(CrtFile.TYPE_NORMAL, false, true)));
        Assert.ThrowsException<NotSupportedException>(() => CrtFile.Load(Crt(1, false, true, new ChipData(0, 0x8000, Rom(0)))));

        var truncated = Crt(CrtFile.TYPE_NORMAL, false, true, new ChipData(0, 0x8000, Rom(0)));
        Assert.ThrowsException<InvalidDataException>(() => CrtFile.Load(truncated[..^100]));
    }

    #endregion

    #region Modi 8K, 16K, Ultimax

    [TestMethod]
    public void Normal8K_ShouldAutostartThroughCbm80Signature()
    {
        var board = WithCartridge(Crt(CrtFile.TYPE_NORMAL, false, true, new ChipData(0, 0x8000, AutostartRoml(5))));

        board.RunCycles(100_000);

        Assert.AreEqual(5, board.Peek(0xD020) & 0x0F);
        Assert.IsTrue(board.Cpu.PC >= 0x800E && board.Cpu.PC <= 0x8010, $"PC={board.Cpu.PC:X4}");
    }

    [TestMethod]
    public void Normal8K_ShouldShowRomlOnlyWithLoramAndHiram_AndKeepBasic()
    {
        var board = WithCartridge(Crt(CrtFile.TYPE_NORMAL, false, true, new ChipData(0, 0x8000, Rom(0x11))));
        byte basicFirstByte = NewBoard().Peek(0xA000);
        board.ParkCpu();

        board.Poke(0x8000, 0x42); // la scrittura va nella RAM sotto la ROM

        Assert.AreEqual(0x11, (int)board.Peek(0x8000));
        Assert.AreEqual(basicFirstByte, board.Peek(0xA000));
        board.SetCpuPort(0x36); // LORAM = 0
        Assert.AreEqual(0x42, (int)board.Peek(0x8000));
    }

    [TestMethod]
    public void Normal8K_WithoutSignature_ShouldBootBasicWithLessMemory()
    {
        var board = WithCartridge(Crt(CrtFile.TYPE_NORMAL, false, true, new ChipData(0, 0x8000, Rom(0xFF))));

        board.RunCycles(3 * CYCLES_PER_SECOND);

        var screen = board.ScreenLines();
        Assert.AreEqual(" 64K RAM SYSTEM  30719 BASIC BYTES FREE", screen[3]);
        Assert.AreEqual("READY.", screen[5]);
    }

    [TestMethod]
    public void Normal16K_ShouldReplaceBasicWithRomh()
    {
        var rom = new byte[0x4000];
        Array.Fill(rom, (byte)0x11, 0, 0x2000);
        Array.Fill(rom, (byte)0x22, 0x2000, 0x2000);
        var board = WithCartridge(Crt(CrtFile.TYPE_NORMAL, false, false, new ChipData(0, 0x8000, rom)));
        board.ParkCpu();

        Assert.AreEqual(0x11, (int)board.Peek(0x8000));
        Assert.AreEqual(0x22, (int)board.Peek(0xA000));

        board.SetCpuPort(0x36); // LORAM = 0: ROML sparisce, ROMH resta (HIRAM = 1)
        Assert.AreEqual(0x00, (int)board.Peek(0x8000));
        Assert.AreEqual(0x22, (int)board.Peek(0xA000));
    }

    [TestMethod]
    public void Ultimax_ShouldResetFromRomhAndDisconnectRam()
    {
        // ROMH a $E000: LDA #7 : STA $D020 : JMP $E005, vettore di reset a $E000
        var romh = Rom(0xEA, 0xA9, 0x07, 0x8D, 0x20, 0xD0, 0x4C, 0x05, 0xE0);
        romh[0x1FFC] = 0x00;
        romh[0x1FFD] = 0xE0;
        romh[0x1000] = 0x5A; // $F000: visibile anche al VIC-II a $3000
        var board = WithCartridge(Crt(CrtFile.TYPE_NORMAL, true, false, new ChipData(0, 0xE000, romh)));

        board.RunCycles(1000);

        Assert.AreEqual(7, board.Peek(0xD020) & 0x0F);
        board.Poke(0x2000, 0x42);
        Assert.AreEqual(0xFF, (int)board.Peek(0x2000)); // area scollegata
        Assert.AreEqual(0x5A, (int)board.VicRead(0x3000));
    }

    [TestMethod]
    public void RemoveCartridge_ShouldRestoreNormalBoot()
    {
        var board = WithCartridge(Crt(CrtFile.TYPE_NORMAL, false, true, new ChipData(0, 0x8000, AutostartRoml(5))));
        board.RunCycles(100_000);

        board.RemoveCartridge();
        board.RunCycles(3 * CYCLES_PER_SECOND);

        Assert.AreEqual(" 64K RAM SYSTEM  38911 BASIC BYTES FREE", board.ScreenLines()[3]);
    }

    #endregion

    #region Cambio di banco

    [TestMethod]
    public void MagicDesk_ShouldSwitchBanksAndDisableWithBit7()
    {
        var board = WithCartridge(Crt(CrtFile.TYPE_MAGIC_DESK, false, true,
            new ChipData(0, 0x8000, Rom(0x10)), new ChipData(1, 0x8000, Rom(0x11))));
        byte basicFirstByte = NewBoard().Peek(0xA000);
        board.ParkCpu();

        Assert.AreEqual(0x10, (int)board.Peek(0x8000));
        board.Poke(0xDE00, 0x01);
        Assert.AreEqual(0x11, (int)board.Peek(0x8000));

        board.Poke(0xDE00, 0x80); // cartuccia scollegata
        Assert.AreEqual(0x00, (int)board.Peek(0x8000));
        Assert.AreEqual(basicFirstByte, board.Peek(0xA000));
    }

    [TestMethod]
    public void Ocean_ShouldShowSelectedBankAtRomlAndRomh()
    {
        var chips = Enumerable.Range(0, 4).Select(b => new ChipData(b, 0x8000, Rom((byte)(0x20 + b)))).ToArray();
        var board = WithCartridge(Crt(CrtFile.TYPE_OCEAN, false, false, chips));
        board.ParkCpu();

        board.Poke(0xDE00, 0x02);

        Assert.AreEqual(0x22, (int)board.Peek(0x8000));
        Assert.AreEqual(0x22, (int)board.Peek(0xA000));
    }

    [TestMethod]
    public void System3_ShouldSelectBankFromWrittenAddress()
    {
        var chips = Enumerable.Range(0, 4).Select(b => new ChipData(b, 0x8000, Rom((byte)(0x30 + b)))).ToArray();
        var board = WithCartridge(Crt(CrtFile.TYPE_SYSTEM3, false, true, chips));
        board.ParkCpu();

        board.Poke(0xDE03, 0x00);

        Assert.AreEqual(0x33, (int)board.Peek(0x8000));
    }

    [TestMethod]
    public void EasyFlash_ShouldBootUltimaxThenFollowControlRegister()
    {
        var romh0 = Rom(0x50);
        romh0[0x1FFC] = 0x00; // reset su un loop: JMP $E000 (4C 00 E0)
        romh0[0x1FFD] = 0xE0;
        romh0[0] = 0x4C; romh0[1] = 0x00; romh0[2] = 0xE0;
        var board = WithCartridge(Crt(CrtFile.TYPE_EASYFLASH, true, false,
            new ChipData(0, 0x8000, Rom(0x40)), new ChipData(0, 0xE000, romh0),
            new ChipData(1, 0x8000, Rom(0x41)), new ChipData(1, 0xE000, Rom(0x51))));
        byte basicFirstByte = NewBoard().Peek(0xA000);
        board.RunCycles(100);

        Assert.AreEqual(0x50, (int)board.Peek(0xE003)); // Ultimax: ROMH a $E000

        board.Poke(0xDE02, 0x07); // modo 16K
        board.ParkCpu();
        Assert.AreEqual(0x40, (int)board.Peek(0x8000));
        Assert.AreEqual(0x50, (int)board.Peek(0xA003));

        board.Poke(0xDE00, 0x01);
        Assert.AreEqual(0x41, (int)board.Peek(0x8000));
        Assert.AreEqual(0x51, (int)board.Peek(0xA000));

        board.Poke(0xDF10, 0x99); // 256 byte di RAM a $DF00
        Assert.AreEqual(0x99, (int)board.Peek(0xDF10));

        board.Poke(0xDE02, 0x04); // GAME ed EXROM inattive: cartuccia invisibile
        Assert.AreEqual(basicFirstByte, board.Peek(0xA000));
    }

    #endregion
}
