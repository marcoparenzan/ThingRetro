using System.Text;

namespace ThingOS64.Tests;

// A C64 with the ThingOS ROMs in its sockets, run cycle by cycle without the real-time clock.
// The ROMs are built from the sources once per test run
static class OsBoard
{
    public const long CYCLES_PER_FRAME = 63 * 312;  // PAL
    public const long CYCLES_PER_SECOND = 985248;

    public static readonly Lazy<OsRoms> Roms = new(() => OsRoms.Build(OsRoms.FindRomFolder()));

    public static int Symbol(string name) => Roms.Value.Symbols[name];

    public static Board New() =>
        new(new SystemRoms(Roms.Value.Kernal, Roms.Value.Basic, Roms.Value.Chargen), startClock: false);

    public static Board Booted()
    {
        var board = New();
        board.RunCycles(CYCLES_PER_SECOND / 2);
        return board;
    }

    extension(Board board)
    {
        public void RunFrames(int frames) => board.RunCycles(frames * CYCLES_PER_FRAME);

        public byte Peek(int address) => board.Address((ushort)address);

        public int PeekWord(int address) => board.Peek(address) | board.Peek(address + 1) << 8;

        // A row of the screen as text: the OS writes ASCII, with bit 7 for the reversed characters
        public string ScreenRow(int row)
        {
            var text = new StringBuilder();
            for (int column = 0; column < 40; column++)
            {
                int code = board.Peek(0x0400 + row * 40 + column) & 0x7F;
                text.Append(code is >= 0x20 and < 0x7F ? (char)code : '·');
            }
            return text.ToString();
        }

        public int ScreenCode(int column, int row) => board.Peek(0x0400 + row * 40 + column);

        public int ColorAt(int column, int row) => board.Peek(0xD800 + row * 40 + column) & 0x0F;

        // Presses the keys for some frames, then releases them for some more: the OS sees one key going down
        public void Press(params C64Key[] keys)
        {
            board.Keyboard.SetPressedKeys(keys);
            board.RunFrames(3);
            board.Keyboard.ReleaseAll();
            board.RunFrames(3);
        }
    }
}
