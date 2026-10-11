namespace Fake64;

public partial class MOS6510
{
    bool nmiPending = false; // NMI è edge-triggered: resta in sospeso finché non viene servito

    public void TriggerNMI()
    {
        nmiPending = true;
    }

    // IRQ è level-triggered: la linea viene letta dalla board (CIA1 + VIC-II)
    // a ogni confine di istruzione, quindi non può andare persa se il flag I è attivo
    private bool IrqLine => board.IrqLine;

    private void HandleIRQ()
    {
        Push((byte)(PC >> 8)); // Salva il PC (byte alto)
        Push((byte)PC);        // Salva il PC (byte basso)
        Push((byte)((Status & ~FLAG_B) | FLAG_U)); // Salva lo stato senza il flag Break
        Status |= FLAG_I;      // Imposta il flag Interrupt Disable
        PC = ReadWord(0xFFFE); // Salta al vettore IRQ
    }

    private void HandleNMI()
    {
        Push((byte)(PC >> 8)); // Salva il PC (byte alto)
        Push((byte)PC);        // Salva il PC (byte basso)
        Push((byte)((Status & ~FLAG_B) | FLAG_U)); // Salva lo stato senza il flag Break
        Status |= FLAG_I;      // Imposta il flag Interrupt Disable
        PC = ReadWord(0xFFFA); // Salta al vettore NMI
    }

}
