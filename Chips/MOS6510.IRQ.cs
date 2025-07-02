using System;
using System.Collections.Generic;

namespace Chips;

public partial class MOS6510
{
    bool irqPending = false; // Indica se un IRQ è in sospeso

    public void TriggerIRQ()
    {
        irqPending = true;
    }

    public void TriggerNMI()
    {
        HandleNMI(); // Gli NMI sono immediati e non mascherabili
    }

    private void HandleIRQ()
    {
        if ((Status & FLAG_I) != 0) return; // Ignora se Interrupt Disable è attivo

        Push((byte)(PC >> 8)); // Salva il PC (byte alto)
        Push((byte)PC);        // Salva il PC (byte basso)
        Push((byte)(Status & ~FLAG_B)); // Salva lo stato senza il flag Break
        Status |= FLAG_I;      // Imposta il flag Interrupt Disable
        PC = ReadWord(0xFFFE); // Salta al vettore IRQ
    }

    private void HandleNMI()
    {
        Push((byte)(PC >> 8)); // Salva il PC (byte alto)
        Push((byte)PC);        // Salva il PC (byte basso)
        Push((byte)(Status & ~FLAG_B)); // Salva lo stato senza il flag Break
        Status |= FLAG_I;      // Imposta il flag Interrupt Disable
        PC = ReadWord(0xFFFA); // Salta al vettore NMI
    }


}