using System;
using System.Collections.Generic;

namespace Fake64;

public partial class MOS6510
{
    private ushort ZeroPage(byte addr)
    {
        return (ushort)(addr & 0xFF);
    }

    private ushort ZeroPageX(byte addr)
    {
        return (ushort)((addr + X) & 0xFF);
    }

    private ushort ZeroPageY(byte addr)
    {
        return (ushort)((addr + Y) & 0xFF);
    }

    private ushort Absolute(ushort addr)
    {
        return addr;
    }

    private ushort AbsoluteX(ushort addr, out bool pageCrossed)
    {
        ushort result = (ushort)(addr + X);
        pageCrossed = (addr & 0xFF00) != (result & 0xFF00);
        return result;
    }

    private ushort AbsoluteY(ushort addr, out bool pageCrossed)
    {
        ushort result = (ushort)(addr + Y);
        pageCrossed = (addr & 0xFF00) != (result & 0xFF00);
        return result;
    }

    private ushort Relative(byte offset)
    {
        sbyte signedOffset = (sbyte)offset;
        return (ushort)(PC + signedOffset);
    }
    private ushort ReadWordIndirect(ushort addr)
    {
        // Handle the 6502's page boundary bug for indirect addressing
        byte low = ReadByte(addr);
        byte high = ReadByte((ushort)((addr & 0xFF) == 0xFF ? addr & 0xFF00 : addr + 1));
        return (ushort)((high << 8) | low);
    }
    private ushort IndirectIndexedY(byte zpAddr)
    {
        ushort baseAddr = ReadWord((ushort)(zpAddr & 0xFF)); // Legge il puntatore dalla zero page
        return (ushort)(baseAddr + Y); // Aggiunge il valore del registro Y
    }

    private ushort IndexedIndirectX(byte zpAddr)
    {
        byte effectiveAddr = (byte)((zpAddr + X) & 0xFF); // Calcola l'indirizzo effettivo nella zero page
        return ReadWord((ushort)(effectiveAddr & 0xFF)); // Legge il puntatore dalla zero page
    }
}