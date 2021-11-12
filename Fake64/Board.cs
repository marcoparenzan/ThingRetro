using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fake64
{
    // original 8 8Kb RAM chip addressing
    internal static partial class Board
    {
        public static byte Address(int addr) => addr switch
        {
            < Chips.chargen_base => Chips.ram[addr >> 13][addr & 0x1FFF],
            < Chips.kernal_base => Chips.chargen[addr - Chips.chargen_base],
            _ => Chips.kernal[addr - Chips.kernal_base],
            //_ => Chips.ram[addr >> 13][addr & 0x1FFF] // depends on 0x0001 address on 6510 chip
        };

        public static byte Address(int addr, byte value) => Chips.ram[addr >> 13][addr & 0x1FFF] = value;
    }
}
