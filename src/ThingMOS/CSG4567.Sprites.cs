namespace ThingMOS;

// Sprite DMA accesses. Each sprite has two fixed cycles in the line to read its pointer and 3 data bytes
// (sprites 0-2 in cycles 58-63, sprites 3-7 in cycles 1-10 of the next line, counting from 1). If the sprite's
// DMA is on, those cycles belong to the VIC-II: BA goes low 3 cycles earlier, AEC in the two access cycles.
// So a sprite costs the CPU 2 cycles per line plus the 3 of warning (fewer, when it is next to another active
// sprite).
//
// When the DMA is on is decided by a counter per sprite (MCBASE), as in the chip:
// - cycles 55 and 56: if the sprite is enabled and its Y matches the low 8 bits of the raster, the DMA starts
//   and MCBASE goes back to 0;
// - cycle 58: if the DMA is on and Y matches, the sprite becomes visible; MCBASE tells which data row
//   (0-62, in steps of 3) will be read and shown on the next raster line;
// - cycles 15-16: MCBASE advances by 3; when it reaches 63 the DMA and the display are turned off.
// With vertical expansion a flip-flop lets MCBASE advance only every other line.
// Since everything depends on the counters and not on Y, changing Y while the sprite is being shown does not
// move it: that is what makes multiplexers possible (more than 8 sprites by reusing the same ones)
public partial class CSG4567
{
    // Cycles (from 0) where BA and AEC are low for each sprite with DMA on, as bit masks
    static readonly byte[] SpriteBaWindow = new byte[CYCLES_PER_LINE];
    static readonly byte[] SpriteAecWindow = new byte[CYCLES_PER_LINE];

    static CSG4567()
    {
        for (int n = 0; n < 8; n++)
        {
            int access = n < 3 ? 57 + 2 * n : 2 * n - 6;   // first access cycle, from 0: sprite 0 = 57, sprite 3 = 0
            for (int c = access - 3; c <= access + 1; c++)
                SpriteBaWindow[(c + CYCLES_PER_LINE) % CYCLES_PER_LINE] |= (byte)(1 << n);
            for (int c = access; c <= access + 1; c++)
                SpriteAecWindow[c % CYCLES_PER_LINE] |= (byte)(1 << n);
        }
    }

    byte spriteDma;                         // sprites with DMA on
    byte spriteDisplay;                     // visible sprites
    byte spriteExpandFlipFlop;              // one bit per sprite: when 1, MCBASE advances
    readonly int[] spriteMcBase = new int[8];

    // What to show on the current line and on the next one (latched at cycle 58 of the previous line)
    byte spriteVisible, spriteVisibleNext;
    readonly int[] spriteData = new int[8], spriteDataNext = new int[8];  // MC: offset of the 3 bytes in the sprite's block

    void ResetSprites()
    {
        spriteDma = spriteDisplay = spriteVisible = spriteVisibleNext = 0;
        spriteExpandFlipFlop = 0xFF;
        Array.Clear(spriteMcBase);
        Array.Clear(spriteData);
        Array.Clear(spriteDataNext);
    }

    byte SpriteDmaCycles(byte[] window) => (byte)(spriteDma & window[currentCycle]);

    // Start of a cycle (from 0): the steps of the sprite logic that fall in this cycle
    void SpriteCycle()
    {
        byte enabled = registers[REG_SPRITE_ENABLE];
        byte expandY = registers[REG_SPRITE_Y_EXPAND];

        switch (currentCycle)
        {
            case 15: // cycle 16: MCBASE advances (by 3, the two steps of cycles 15 and 16 together)
                spriteExpandFlipFlop |= (byte)~expandY;   // without expansion the flip-flop stays at 1
                for (int n = 0; n < 8; n++)
                {
                    int bit = 1 << n;
                    if ((spriteDma & bit) == 0 || (spriteExpandFlipFlop & bit) == 0) continue;
                    spriteMcBase[n] += 3;
                    if (spriteMcBase[n] >= 63)
                    {
                        spriteDma &= (byte)~bit;
                        spriteDisplay &= (byte)~bit;
                    }
                }
                break;

            case 54: // cycle 55: the expansion flip-flop toggles at every line, then the Y check
                spriteExpandFlipFlop ^= expandY;
                StartSpriteDma(enabled, expandY);
                break;

            case 55: // cycle 56: second check (sees the writes made in cycle 55)
                StartSpriteDma(enabled, expandY);
                break;

            case 57: // cycle 58: display and data row for the next raster line
                for (int n = 0; n < 8; n++)
                {
                    int bit = 1 << n;
                    if ((spriteDma & bit) != 0 && registers[REG_SPRITE0_Y + n * 2] == (rasterLine & 0xFF))
                        spriteDisplay |= (byte)bit;
                    spriteDataNext[n] = spriteMcBase[n];
                }
                spriteVisibleNext = spriteDisplay;
                break;
        }
    }

    void StartSpriteDma(byte enabled, byte expandY)
    {
        for (int n = 0; n < 8; n++)
        {
            int bit = 1 << n;
            if ((enabled & bit) == 0 || (spriteDma & bit) != 0) continue;
            if (registers[REG_SPRITE0_Y + n * 2] != (rasterLine & 0xFF)) continue;

            spriteDma |= (byte)bit;
            spriteMcBase[n] = 0;
            if ((expandY & bit) != 0) spriteExpandFlipFlop &= (byte)~bit;
        }
    }

    // Start of a new line: the data latched at cycle 58 of the previous one apply
    void LatchSprites()
    {
        spriteVisible = spriteVisibleNext;
        Array.Copy(spriteDataNext, spriteData, 8);
    }
}
