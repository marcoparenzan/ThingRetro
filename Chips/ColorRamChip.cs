using System;
using System.Collections.Generic;
using System.Text;

namespace Chips;
public class ColorRamChip(Board board)
{
    object bus_lock = new();
    byte[] bytes = new byte[0x0400];

    public byte Address(ushort addr)
    {
        lock (bus_lock)
        {
            return bytes[addr];
        }
    }

    public void Address(ushort addr, byte value)
    {
        lock (bus_lock)
        {
            bytes[addr] = value;
        }
    }
}
