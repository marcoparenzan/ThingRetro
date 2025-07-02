using System;
using System.Collections.Generic;

namespace Fake64;

public partial class MOS6510
{
    private void SimulateIO()
    {
        // Simula un comportamento per i registri di I/O
        ioRegister0 = (byte)(ioRegister0 + 1); // Incrementa il valore come esempio
        ioRegister1 = (byte)(ioRegister1 ^ 0xFF); // Inverte i bit come esempio
    }

    private byte ReadIORegister(ushort addr)
    {
        switch (addr)
        {
            case 0x0000: return ioRegister0;
            case 0x0001: return ioRegister1;
            default: throw new InvalidOperationException($"Invalid I/O register address: {addr:X4}");
        }
    }

    private void WriteIORegister(ushort addr, byte value)
    {
        switch (addr)
        {
            case 0x0000:
                ioRegister0 = value;
                //UpdateMemoryMapping();
                break;
            case 0x0001:
                ioRegister1 = value;
                //SimulatePeripheralInteraction();
                break;
            default:
                throw new InvalidOperationException($"Invalid I/O register address: {addr:X4}");
        }
    }


}