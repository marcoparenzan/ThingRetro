namespace Fake64;

public partial class MOS6510
{
    // Eccezione personalizzata per opcode sconosciuti
    public class UnknownOpcodeException : Exception
    {
        public UnknownOpcodeException(byte opcode, ushort pc)
            : base($"Unknown opcode: {opcode:X2} at {pc:X4}. Check memory or program logic.")
        {
        }
    }
}