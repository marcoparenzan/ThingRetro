namespace Fake64.Prg;

// Buffer di tastiera del KERNAL: il testo inserito qui viene letto come se fosse stato digitato.
// Serve per avviare un programma caricato ("RUN\r") senza toccare la matrice della tastiera
public static class KeyboardBuffer
{
    const ushort BUFFER = 0x0277;
    const ushort COUNT = 0x00C6;
    public const int SIZE = 10;

    // "\r" = RETURN; al massimo 10 caratteri, come il buffer del KERNAL
    public static void TypeText(this Board board, string text)
    {
        if (text.Length > SIZE)
            throw new ArgumentException($"Il buffer di tastiera contiene al massimo {SIZE} caratteri", nameof(text));

        var ram = board.Ram;
        for (int i = 0; i < text.Length; i++)
            ram.Address((ushort)(BUFFER + i), (byte)char.ToUpperInvariant(text[i]));
        ram.Address(COUNT, (byte)text.Length);
    }
}
