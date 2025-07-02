using System;
using System.Collections.Generic;

namespace Chips;

public partial class MOS6510
{
    private ushort ReadWordIndirect(ushort addr)
    {
        // Handle the 6502's page boundary bug for indirect addressing
        byte low = ReadByte(addr);
        byte high = ReadByte((ushort)((addr & 0xFF) == 0xFF ? addr & 0xFF00 : addr + 1));
        return (ushort)((high << 8) | low);
    }
    private ushort IndirectIndexed(byte zpAddr)
    {
        ushort baseAddr = ReadWord((ushort)(zpAddr & 0xFF)); // Legge il puntatore dalla zero page
        return (ushort)(baseAddr + Y); // Aggiunge il valore del registro Y
    }

    private ushort IndexedIndirect(byte zpAddr)
    {
        byte effectiveAddr = (byte)((zpAddr + X) & 0xFF); // Calcola l'indirizzo effettivo nella zero page
        return ReadWord((ushort)(effectiveAddr & 0xFF)); // Legge il puntatore dalla zero page
    }
}