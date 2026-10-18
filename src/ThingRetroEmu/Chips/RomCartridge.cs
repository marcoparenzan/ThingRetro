namespace ThingRetroEmu.Chips;

// A plain ROM cartridge for the C65's cartridge port: the image is seen at physical $40000 and following (256 KB at most)
public class RomCartridge : ICartridgePinout
{
    public const int MAX_SIZE = 0x40000;

    readonly byte[] image;

    public RomCartridge(byte[] image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Length == 0 || image.Length > MAX_SIZE)
            throw new ArgumentException($"A cartridge has between 1 byte and {MAX_SIZE} bytes of ROM", nameof(image));
        this.image = image;
    }

    public int Size => image.Length;

    public byte Read(int offset) => offset < image.Length ? image[offset] : (byte)0xFF;
}
