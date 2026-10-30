namespace ThingMOS;

public partial class MOS6502
{
    protected void SampleInputPins() { SampleNmi(); SampleSo(); }
    protected BusCycle? PreviewBusCycle()
    {
        if (jammed || (cycle != 0 && instruction.Op == Op.JAM)) return null;
        ushort next = PC;
        if (cycle == 0 || (!inInterrupt && instruction.Kind is Kind.Implied or Kind.Branch))
            return new(next, false);
        if (inInterrupt || (instruction.Kind == Kind.Special && instruction.Op == Op.BRK))
            next = cycle switch
            {
                >= 2 and <= 4 => (ushort)(0x100 + SP),
                5 => vectorIsNmi ? (ushort)0xfffa : (ushort)0xfffe,
                6 => vectorIsNmi ? (ushort)0xfffb : (ushort)0xffff,
                _ => PC
            };
        else if (instruction.Kind == Kind.Special)
            next = instruction.Op switch
            {
                Op.JMP when instruction.Mode != Mode.Abs && cycle == 3 => (ushort)((operandHigh << 8) | operandLow),
                Op.JMP when instruction.Mode != Mode.Abs && cycle == 4 => (ushort)((operandHigh << 8) | (byte)(operandLow + 1)),
                Op.JSR when cycle is >= 2 and <= 4 => (ushort)(0x100 + SP),
                Op.RTS or Op.RTI when cycle == 2 => (ushort)(0x100 + SP),
                Op.RTS when cycle is 3 or 4 => (ushort)(0x100 + (byte)(SP + 1)),
                Op.RTI when cycle >= 3 => (ushort)(0x100 + (byte)(SP + 1)),
                Op.PHA or Op.PHP when cycle == 2 => (ushort)(0x100 + SP),
                Op.PLA or Op.PLP when cycle == 2 => (ushort)(0x100 + SP),
                Op.PLA or Op.PLP when cycle == 3 => (ushort)(0x100 + (byte)(SP + 1)),
                _ => PC
            };
        else if (accessCycle != 0 && cycle >= accessCycle)
        {
            next = address;
            if (pageCrossed && instruction.Kind == Kind.Write && instruction.Op is Op.SHA or Op.SHX or Op.SHY or Op.TAS)
            {
                byte high = (byte)((baseAddress >> 8) + 1);
                byte value = instruction.Op switch
                {
                    Op.SHX => (byte)(X & high), Op.SHY => (byte)(Y & high),
                    _ => (byte)(A & X & high)
                };
                next = (ushort)((value << 8) | (address & 255));
            }
        }
        else next = instruction.Mode switch
        {
            Mode.ZpX or Mode.ZpY when cycle == 2 => address,
            Mode.AbsX or Mode.AbsY when cycle >= 3 => (ushort)((baseAddress & 0xff00) | (address & 255)),
            Mode.IndX when cycle is 2 or 3 => pointer,
            Mode.IndX when cycle == 4 => (byte)(pointer + 1),
            Mode.IndY when cycle == 2 => pointer,
            Mode.IndY when cycle == 3 => (byte)(pointer + 1),
            Mode.IndY when cycle >= 4 => (ushort)((baseAddress & 0xff00) | (address & 255)),
            _ => PC
        };
        return new(next, IsWriteCycle());
    }
}
