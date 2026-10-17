using Thing65.C65;

namespace Thing65.Prg;

// The KERNAL keyboard buffer: text put here is read as if it had been typed.
// Used to start a loaded program ("RUN\r") without touching the keyboard matrix
public static class KeyboardBuffer
{
    const ushort BUFFER = 0x0277;
    const ushort COUNT = 0x00C6;
    public const int SIZE = 10;

    // "\r" = RETURN; at most 10 characters, like the KERNAL buffer
    public static void TypeText(this Board board, string text)
    {
        if (text.Length > SIZE)
            throw new ArgumentException($"The keyboard buffer holds at most {SIZE} characters", nameof(text));

        var ram = board.Ram;
        for (int i = 0; i < text.Length; i++)
            ram.Address((ushort)(BUFFER + i), (byte)char.ToUpperInvariant(text[i]));
        ram.Address(COUNT, (byte)text.Length);
    }
}
