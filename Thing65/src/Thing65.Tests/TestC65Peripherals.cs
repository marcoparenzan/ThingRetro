using Thing65.WinForms;

namespace Thing65.Tests;

// What can be fitted to the C65 beyond the basic machine: RESTORE and CAPS LOCK keys, the UART, the RAM expansion
// card and the cartridge port
[TestClass]
public sealed class C65PeripheralsTests
{
    static BoardC65 NewBoard()
    {
        var image = new byte[SystemRom128.SIZE];
        image[0xFFFC] = 0x00; // reset vector: $E000
        image[0xFFFD] = 0xE0;
        var board = new BoardC65(new SystemRom128(image), startClock: false);
        board.CpuSocket.Remove(); // the test drives the bus itself
        return board;
    }

    static void EnterVic3Mode(BoardC65 board)
    {
        board.CpuWrite(0xD02F, 0xA5);
        board.CpuWrite(0xD02F, 0x96);
    }

    #region Keys

    [TestMethod]
    public void RestoreKey_ShouldPullTheNmiLineLowWhilePressed()
    {
        var board = NewBoard();
        Assert.IsFalse(board.NmiLine);

        board.Keyboard.SetPressedKeys([C65Key.Restore]);
        Assert.IsTrue(board.NmiLine);
        Assert.IsTrue(board.Keyboard.IsPressed(C65Key.Restore));

        board.Keyboard.SetPressedKeys([]);
        Assert.IsFalse(board.NmiLine);
    }

    [TestMethod]
    public void RestoreKey_ShouldNotDisturbTheMatrix()
    {
        var board = NewBoard();

        board.Keyboard.SetPressedKeys([C65Key.Restore, C65Key.A]);

        Assert.IsTrue(board.Keyboard.IsPressed(C65Key.A));
        Assert.AreEqual(0xFF, board.Keyboard.GetKeyboardState(0xFF)); // no row selected
    }

    [TestMethod]
    public void CapsLock_ShouldBeSensedAtBitZeroOfD607AndStayDown()
    {
        var board = NewBoard();
        EnterVic3Mode(board);
        Assert.AreEqual(1, board.CpuRead(0xD607) & 1);

        board.Keyboard.CapsLock = true;
        board.Keyboard.ReleaseAll(); // other keys going up do not release a locking key

        Assert.AreEqual(0, board.CpuRead(0xD607) & 1);
        board.Keyboard.CapsLock = false;
        Assert.AreEqual(1, board.CpuRead(0xD607) & 1);
    }

    [TestMethod]
    public void PcKeyboard_ShouldMapPageUpToRestore()
    {
        var keys = PcKeyboardLayout.ToC65Keys([Keys.PageUp]);

        CollectionAssert.AreEquivalent(new[] { C65Key.Restore }, keys.ToArray());
    }

    #endregion

    #region UART

    [TestMethod]
    public void Uart_ShouldSendWhatIsWrittenWhenTheTransmitterIsOn()
    {
        var uart = new C65Uart();
        var sent = new List<byte>();
        uart.Transmit = sent.Add;

        uart.Write(0, 0x41);          // transmitter off: nothing leaves
        uart.Write(2, 0x80);
        uart.Write(0, 0x42);

        CollectionAssert.AreEqual(new byte[] { 0x42 }, sent);
        Assert.AreNotEqual(0, uart.Read(1) & 0x40, "transmitter empty");
    }

    [TestMethod]
    public void Uart_ShouldReceiveACharacterAndReportOverrun()
    {
        var uart = new C65Uart();
        uart.Write(2, 0x40);

        uart.Receive(0x31);
        Assert.AreEqual(1, uart.Read(1) & 1, "FULL");
        uart.Receive(0x32);
        Assert.AreEqual(2, uart.Read(1) & 2, "OVERRUN");

        Assert.AreEqual(0x31, uart.Read(0), "the first character is kept");
        Assert.AreEqual(0, uart.Read(1) & 3, "reading clears the flags");
    }

    [TestMethod]
    public void Uart_WithReceiverOff_ShouldIgnoreCharacters()
    {
        var uart = new C65Uart();

        uart.Receive(0x31);

        Assert.AreEqual(0, uart.Read(1) & 1);
    }

    [TestMethod]
    public void Uart_ShouldRaiseIrqOrNmiAccordingToTheMask()
    {
        var uart = new C65Uart();
        uart.Write(2, 0x40);
        uart.Write(5, 0x40);          // receiver IRQ
        Assert.IsFalse(uart.Irq);

        uart.Receive(0x55);

        Assert.IsTrue(uart.Irq);
        Assert.IsFalse(uart.Nmi);
        Assert.AreEqual(0x40, uart.Read(6));
        uart.Write(5, 0x10);          // receiver NMI instead
        Assert.IsFalse(uart.Irq);
        Assert.IsTrue(uart.Nmi);
        uart.Read(0);
        Assert.IsFalse(uart.Nmi);
    }

    [TestMethod]
    public void Uart_ShouldBeReachedAtD600_AndItsInterruptReachTheCpuLines()
    {
        var board = NewBoard();
        EnterVic3Mode(board);
        board.CpuWrite(0xD602, 0x40);
        board.CpuWrite(0xD605, 0x40);
        var uart = board.UartSocket.Chip!;
        Assert.IsFalse(board.IrqLine);

        uart.Receive(0x7E);

        Assert.IsTrue(board.IrqLine);
        Assert.AreEqual(0x7E, board.CpuRead(0xD600));
        Assert.IsFalse(board.IrqLine);
    }

    #endregion

    #region RAM expansion

    [TestMethod]
    public void WithoutTheCard_TheExpansionAreaShouldReadAsNothing()
    {
        var board = NewBoard();

        board.Bus.Write(0x80000, true, 0x12);

        Assert.AreEqual(0xFF, board.Bus.Read(0x80000, true));
        Assert.IsTrue(board.RamExpansionSocket.IsEmpty);
    }

    [TestMethod]
    public void RamExpansion_ShouldGiveTwoBanksOfTheCpuSelectedByTheControlRegister()
    {
        var board = NewBoard();
        board.RamExpansionSocket.Insert(new RamExpansion());
        EnterVic3Mode(board);

        board.Bus.Write(0x80010, true, 0xA1);
        board.CpuWrite(0xD0A0, 0x08);                    // CPU bank 1
        Assert.AreEqual(0x00, board.Bus.Read(0x80010, true));
        board.Bus.Write(0xFFFFF, true, 0xB2);
        board.CpuWrite(0xD0A0, 0x00);

        Assert.AreEqual(0xA1, board.Bus.Read(0x80010, true));
        Assert.AreEqual(0x00, board.Bus.Read(0xFFFFF, true));
        board.CpuWrite(0xD0A0, 0x08);
        Assert.AreEqual(0xB2, board.Bus.Read(0xFFFFF, true));
    }

    [TestMethod]
    public void RamExpansion_ShouldNotBeSeenByTheCpuBelowItsArea()
    {
        var board = NewBoard();
        board.RamExpansionSocket.Insert(new RamExpansion());

        board.Bus.Write(0x7FFFF, true, 0x55);

        Assert.AreEqual(0xFF, board.Bus.Read(0x7FFFF, true), "the cartridge area, empty");
    }

    [TestMethod]
    public void RamExpansion_Reset_ShouldClearTheControlRegisterButKeepTheRam()
    {
        var board = NewBoard();
        board.RamExpansionSocket.Insert(new RamExpansion());
        EnterVic3Mode(board);
        board.Bus.Write(0x80000, true, 0x77);
        board.CpuWrite(0xD0A0, 0x0F);

        board.Reset();

        Assert.AreEqual(0x77, board.Bus.Read(0x80000, true));
    }

    [TestMethod]
    public void RamExpansion_ShouldShowTheVicAWindowOfABank()
    {
        var card = new RamExpansion();
        card.WriteControl(0x08);                         // the CPU writes bank 1
        card.Write(0xE0005, 0xCD);                       // bank 1, range $E0000
        card.Write(0xC0005, 0xAB);

        card.WriteControl(0x04 | 0x02 | 0x01);           // VIC: bank 1, range $E0000
        Assert.IsTrue(card.VicEnabled);
        Assert.AreEqual(0xCD, card.ReadVic(5));
        card.WriteControl(0x04 | 0x01);                  // range $C0000
        Assert.AreEqual(0xAB, card.ReadVic(5));
        card.WriteControl(0x04);                         // bank 0
        Assert.AreEqual(0x00, card.ReadVic(5));
        card.WriteControl(0x00);
        Assert.IsFalse(card.VicEnabled);
    }

    #endregion

    #region Cartridge

    [TestMethod]
    public void Cartridge_ShouldAppearAt40000()
    {
        var board = NewBoard();
        Assert.AreEqual(0xFF, board.Physical(0x40000));

        var image = new byte[0x2000];
        image[0] = 0xC3;
        image[0x1FFF] = 0x3C;
        board.CartridgeSocket.Insert(new RomCartridge(image));

        Assert.AreEqual(0xC3, board.Physical(0x40000));
        Assert.AreEqual(0x3C, board.Physical(0x41FFF));
        Assert.AreEqual(0xFF, board.Physical(0x42000), "past the end of the ROM");
    }

    [TestMethod]
    public void Cartridge_ShouldBeReadOnly()
    {
        var board = NewBoard();
        board.CartridgeSocket.Insert(new RomCartridge([0x11]));

        board.Bus.Write(0x40000, true, 0x99);

        Assert.AreEqual(0x11, board.Physical(0x40000));
    }

    [TestMethod]
    public void Cartridge_Removed_ShouldLeaveTheAreaEmpty()
    {
        var board = NewBoard();
        board.CartridgeSocket.Insert(new RomCartridge([0x11]));

        board.CartridgeSocket.Remove();

        Assert.AreEqual(0xFF, board.Physical(0x40000));
    }

    [TestMethod]
    public void Cartridge_WithAnInvalidSize_ShouldBeRefused()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new RomCartridge([]));
        Assert.ThrowsExactly<ArgumentException>(() => new RomCartridge(new byte[RomCartridge.MAX_SIZE + 1]));
    }

    #endregion
}
