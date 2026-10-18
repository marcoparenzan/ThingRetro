namespace Thing65.C65;

// The 128 KB system ROM of the C65 (physical $20000-$3FFFF). The board does not know where it comes from: whoever
// creates the BoardC65 reads the file and passes the contents. The layout (from the C65 manual):
//   $20000 reserved   $24000 reserved   $28000 C65 character set at $29000, C64 BASIC at $2A000
//   $2C000 interface (maps at $C000)    $2D000 C64 character set   $2E000 C64 KERNAL
//   $30000 DOS and monitor    $38000 BASIC 10 and graphics   $3E000 C65 KERNAL
public sealed record SystemRom128
{
    public const int SIZE = 0x20000;

    public SystemRom128(byte[] image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Length != SIZE)
            throw new ArgumentException($"The C65 system ROM must be {SIZE} bytes, not {image.Length}", nameof(image));
        Image = image;
    }

    public byte[] Image { get; }
}
