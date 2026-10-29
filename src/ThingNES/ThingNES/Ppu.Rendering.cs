namespace ThingNES;

public sealed partial class Ppu
{
    readonly byte[] pixels = new byte[256 * 240];
    // Live buffer of NES palette indices, updated during visible dots.
    public ReadOnlyMemory<byte> Framebuffer => pixels;

    void RenderPixel(int x, int y)
    {
        int background = 0, backgroundPalette = 0;
        if ((mask & 8) != 0 && (x >= 8 || (mask & 2) != 0))
        {
            int bit = 0x8000 >> fineX;
            background = ((patternLow & bit) != 0 ? 1 : 0) | ((patternHigh & bit) != 0 ? 2 : 0);
            backgroundPalette = ((attributeLow & bit) != 0 ? 1 : 0) | ((attributeHigh & bit) != 0 ? 2 : 0);
        }
        int paletteAddress = background == 0 ? 0x3f00 : 0x3f00 + backgroundPalette * 4 + background;
        if ((mask & 0x10) != 0 && (x >= 8 || (mask & 4) != 0))
        {
            for (int s = 0; s < activeCount; s++)
            {
                ref readonly var sprite = ref activeSprites[s];
                int dx = x - sprite.X, attr = sprite.Attributes;
                if (dx is < 0 or > 7) continue;
                int bit = (attr & 0x40) != 0 ? dx : 7 - dx;
                int color = ((sprite.Low >> bit) & 1) | (((sprite.High >> bit) & 1) << 1);
                if (color == 0) continue;
                if (sprite.Id == 0 && background != 0 && x != 255) status |= 0x40;
                if (background == 0 || (attr & 0x20) == 0) paletteAddress = 0x3f10 + (attr & 3) * 4 + color;
                break; // Earlier opaque sprite wins even when behind the background.
            }
        }
        byte result = ReadMemory(paletteAddress);
        pixels[y * 256 + x] = (byte)(result & ((mask & 1) != 0 ? 0x30 : 0x3f));
    }
}
