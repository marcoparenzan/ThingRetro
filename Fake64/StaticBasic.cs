using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fake64
{
    internal static class StaticBasic
    {
        public static void POKE(ushort addr, byte b) => Chips.ram[addr >> 13][addr & 0x1fff] = b;

        public static byte PEEK(ushort addr) => Chips.ram[addr>>13][addr & 0x1fff];

        public static byte ASC(char ch) => ((byte)ch);
    }
}
