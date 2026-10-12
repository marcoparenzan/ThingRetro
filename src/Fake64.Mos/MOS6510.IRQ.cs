namespace Fake64.Mos;

public partial class MOS6510
{
    bool nmiPending = false; // NMI è edge-triggered: resta in sospeso finché non viene servito
    bool nmiWasLow = false;

    // /NMI scatta sul fronte di discesa: la linea viene campionata a ogni ciclo
    void SampleNmi()
    {
        bool nmiLow = NmiInput();
        if (nmiLow && !nmiWasLow) nmiPending = true;
        nmiWasLow = nmiLow;
    }

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
