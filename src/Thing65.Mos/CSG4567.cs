namespace Thing65.Mos;

// CSG 4567 VIC-III: the C65's video chip. For now it is a MOS 6569 (VIC-II) in a larger package: it adds the register
// file $D02F-$D07F (KEY, control registers A and B, bitplane and DAT registers, kept but not yet acted on) and the
// palette RAM, and decodes the access to its registers. The new video modes (80 columns, bitplanes, 256 colors)
// are not implemented yet.
// Reference: "C65 Specifications", chapter 2.4
public class CSG4567 : I4567Pinout
{
    const int KEY = 0x2F;
    const int CONTROL_A = 0x30;
    const int CONTROL_B = 0x31;
    const int REGISTERS = 0x80;

    readonly MOS6569 vic = new();
    readonly byte[] registers = new byte[REGISTERS]; // $D02F-$D07F (the VIC-II part is in the MOS6569)
    readonly byte[][] palette = [new byte[256], new byte[256], new byte[256]];
    bool keyAfterFirst;                              // $A5 written: waiting for $96

    public bool NewMode { get; private set; }
    public byte ControlA => registers[CONTROL_A];
    public byte ControlB => registers[CONTROL_B];

    // Palette entry as written: 4 bits per component (bit 4 of red is FG/BG)
    public byte Palette(int component, int index) => palette[component][index];

    // ---- Pinout of the VIC-II ---------------------------------------------------------------------------
    public IVicMemory? Memory { get => vic.Memory; set => vic.Memory = value; }
    public Action? FrameCompleted { get => vic.FrameCompleted; set => vic.FrameCompleted = value; }
    public bool Irq => vic.Irq;
    public bool BusAvailable => vic.BusAvailable;
    public bool AddressEnable => vic.AddressEnable;
    public unsafe void Render(byte* pixels) => vic.Render(pixels);
    public void Tick() => vic.Tick();

    public void Reset()
    {
        vic.Reset();
        Array.Clear(registers);
        NewMode = false;
        keyAfterFirst = false;
    }

    // The registers: $00-$2E are the VIC-II's (mirrored every 64 bytes in VIC-II mode), $2F-$7F exist only in
    // VIC-III mode
    public byte Read(int register)
    {
        if (!NewMode) register &= 0x3F;
        if (register <= 0x2E) return vic.Read(register);
        if (!NewMode || register >= REGISTERS) return 0xFF;
        return registers[register];
    }

    public void Write(int register, byte value)
    {
        if (!NewMode) register &= 0x3F;
        if (register <= 0x2E)
        {
            vic.Write(register, value);
            return;
        }
        // The KEY register works in both modes: $A5 then $96 switches to VIC-III mode, anything else back
        if (register == KEY)
        {
            bool enter = keyAfterFirst && value == 0x96;
            keyAfterFirst = value == 0xA5;
            NewMode = enter;
            registers[KEY] = value;
            return;
        }
        if (NewMode && register < REGISTERS) registers[register] = value;
    }

    public void WritePalette(int component, int index, byte value) =>
        palette[component][index & 0xFF] = (byte)(value & 0x1F);
}
