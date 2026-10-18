namespace ThingRetroEmu.Chips;

// The C65 RAM expansion card (REC): 1 MB of RAM in two banks of 512 KB and the REC's four-bit write-only register at $D0A0.
//   bit 3  CPU bank: the CPU (and the DMAgic) see bank 0 or 1 at $80000-$FFFFF
//   bit 2  the VIC-III sees the expansion RAM instead of the internal RAM
//   bit 1  VIC address range: $C0000-$DFFFF (0) or $E0000-$FFFFF (1) of the bank
//   bit 0  VIC bank
// Reference: "C65 Specifications", chapter 2.8
public class RamExpansion : IRamExpansionPinout
{
    public const int BANK_SIZE = 0x80000;
    public const int BASE_ADDRESS = 0x80000;

    readonly byte[] ram = new byte[2 * BANK_SIZE];
    byte control;

    public byte Control => control;

    public void WriteControl(byte value) => control = (byte)(value & 0x0F);

    public void Reset() => control = 0;

    int CpuBank => (control & 8) != 0 ? BANK_SIZE : 0;

    public byte Read(int address) => ram[CpuBank | address & (BANK_SIZE - 1)];
    public void Write(int address, byte value) => ram[CpuBank | address & (BANK_SIZE - 1)] = value;

    public bool VicEnabled => (control & 4) != 0;

    int VicOffset(int address) => (control & 1) * BANK_SIZE | 0x40000 | (control & 2) << 16 | address & 0x1FFFF;

    public byte ReadVic(int address) => ram[VicOffset(address)];
    public void WriteVic(int address, byte value) => ram[VicOffset(address)] = value;
}
