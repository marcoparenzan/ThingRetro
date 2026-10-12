using System;
using System.Collections.Generic;

namespace Fake64.Mos;

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
    private ushort AddX(ushort addr) => AbsoluteX(addr, out pageCrossed);

    private ushort AddY(ushort addr) => AbsoluteY(addr, out pageCrossed);

    private ushort IndirectIndexedY(byte zpAddr)
    {
        // Il puntatore in zero page si avvolge su $FF -> $00
        ushort baseAddr = (ushort)(ReadByte(zpAddr) | (ReadByte((byte)(zpAddr + 1)) << 8));
        return AbsoluteY(baseAddr, out pageCrossed); // Aggiunge il valore del registro Y
    }

    // Opcode che pagano un ciclo extra quando pageCrossed è vero
    static readonly bool[] PageCrossPenalty = BuildPageCrossPenalty();

    static bool[] BuildPageCrossPenalty()
    {
        var table = new bool[256];
        byte[] opcodes =
        [
            // abs,X (letture)
            0x1D, 0x3D, 0x5D, 0x7D, 0xBC, 0xBD, 0xDD, 0xFD,
            // abs,Y (letture)
            0x19, 0x39, 0x59, 0x79, 0xB9, 0xBE, 0xBF, 0xBB, 0xD9, 0xF9,
            // (zp),Y (letture)
            0x11, 0x31, 0x51, 0x71, 0xB1, 0xB3, 0xD1, 0xF1,
            // branch presi verso un'altra pagina
            0x10, 0x30, 0x50, 0x70, 0x90, 0xB0, 0xD0, 0xF0,
        ];
        foreach (var opcode in opcodes) table[opcode] = true;
        return table;
    }

    private ushort IndexedIndirectX(byte zpAddr)
    {
        byte effectiveAddr = (byte)((zpAddr + X) & 0xFF); // Calcola l'indirizzo effettivo nella zero page
        return (ushort)(ReadByte(effectiveAddr) | (ReadByte((byte)(effectiveAddr + 1)) << 8)); // Legge il puntatore dalla zero page
    }
}