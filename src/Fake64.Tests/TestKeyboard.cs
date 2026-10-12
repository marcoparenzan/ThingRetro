using static Fake64.Tests.TestHelpers;

namespace Fake64.Tests;

[TestClass]
public sealed class KeyboardTests
{
    const long CYCLES_PER_SECOND = 985248;
    const int FRAME_CYCLES = 312 * 63;

    // Codici virtual key di Windows
    const int VK_SHIFT = 0x10, VK_LEFT = 0x25, VK_UP = 0x26, VK_RIGHT = 0x27, VK_DOWN = 0x28;
    const int VK_F1 = 0x70, VK_F2 = 0x71, VK_F3 = 0x72, VK_F4 = 0x73, VK_F5 = 0x74, VK_F6 = 0x75, VK_F7 = 0x76, VK_F8 = 0x77;

    // Righe della matrice del C64: una riga si seleziona mettendo a 0 il suo bit della porta A della CIA1
    static byte Row(Board board, int row) => board.Keyboard.GetKeyboardState((byte)~(1 << row));

    static void HoldKeys(Board board, params int[] virtualKeys)
    {
        board.Keyboard.HostKeyDown = vk => virtualKeys.Contains(vk);
        board.Keyboard.ScanHostKeyboard();
    }

    [TestMethod]
    [DataRow(VK_F1, 4, false)]
    [DataRow(VK_F2, 4, true)]
    [DataRow(VK_F3, 5, false)]
    [DataRow(VK_F4, 5, true)]
    [DataRow(VK_F5, 6, false)]
    [DataRow(VK_F6, 6, true)]
    [DataRow(VK_F7, 3, false)]
    [DataRow(VK_F8, 3, true)]
    public void FunctionKeys_ShouldPressMatrixKeyAndShiftForEvenKeys(int virtualKey, int column, bool shifted)
    {
        var board = NewBoard();

        HoldKeys(board, virtualKey);

        Assert.AreEqual(0xFF & ~(1 << column), (int)Row(board, 0));
        Assert.AreEqual(shifted, (Row(board, 1) & 0x80) == 0); // Shift sinistro: riga 1, colonna 7
    }

    [TestMethod]
    [DataRow(VK_RIGHT, 2, false)]
    [DataRow(VK_LEFT, 2, true)]
    [DataRow(VK_DOWN, 7, false)]
    [DataRow(VK_UP, 7, true)]
    public void CursorKeys_ShouldUseCrsrKeysWithShiftForLeftAndUp(int virtualKey, int column, bool shifted)
    {
        var board = NewBoard();

        HoldKeys(board, virtualKey);

        Assert.AreEqual(0xFF & ~(1 << column), (int)Row(board, 0));
        Assert.AreEqual(shifted, (Row(board, 1) & 0x80) == 0);
        Assert.AreEqual(0xFF, (int)Row(board, 6)); // niente più '/' o freccia su
    }

    [TestMethod]
    public void ReleasingKeys_ShouldClearMatrixAndShift()
    {
        var board = NewBoard();
        HoldKeys(board, VK_F2);

        HoldKeys(board);

        for (int row = 0; row < 8; row++)
            Assert.AreEqual(0xFF, (int)Row(board, row));
    }

    [TestMethod]
    public void PcShiftWithF1_ShouldBeF2()
    {
        var board = NewBoard();

        HoldKeys(board, VK_SHIFT, VK_F1);

        Assert.AreEqual(0xFF & ~(1 << 4), (int)Row(board, 0));
        Assert.AreEqual(0, Row(board, 1) & 0x80);
    }

    // Integrazione: il KERNAL scandisce la matrice nell'IRQ e mette il codice PETSCII nel buffer a $0277
    [TestMethod]
    [DataRow(VK_F1, 133)]
    [DataRow(VK_F3, 134)]
    [DataRow(VK_F5, 135)]
    [DataRow(VK_F7, 136)]
    [DataRow(VK_F2, 137)]
    [DataRow(VK_F4, 138)]
    [DataRow(VK_F6, 139)]
    [DataRow(VK_F8, 140)]
    [DataRow(VK_DOWN, 17)]
    [DataRow(VK_UP, 145)]
    [DataRow(VK_RIGHT, 29)]
    [DataRow(VK_LEFT, 157)]
    public void Kernal_ShouldDecodeKeyToPetscii(int virtualKey, int petscii)
    {
        var board = NewBoard();
        board.RunCycles(3 * CYCLES_PER_SECOND);

        // CPU in un loop con gli IRQ abilitati: il KERNAL scandisce la tastiera ma nessuno svuota il buffer
        board.Load(0xC000, 0x4C, 0x00, 0xC0);
        board.Cpu.Status = 0x20;
        board.Poke(0x00C6, 0);

        board.Keyboard.HostKeyDown = vk => vk == virtualKey;
        board.RunCycles(3 * FRAME_CYCLES);
        board.Keyboard.HostKeyDown = _ => false;
        board.RunCycles(3 * FRAME_CYCLES);

        Assert.IsTrue(board.Peek(0x00C6) >= 1, "nessun tasto nel buffer");
        Assert.AreEqual(petscii, (int)board.Peek(0x0277));
    }
}
