namespace ThingNES;

public sealed partial class Ppu
{
    readonly byte[] secondaryOam = new byte[32];
    readonly int[] secondaryIds = new int[8];
    readonly SpriteRow[] activeSprites = new SpriteRow[8], fetchedSprites = new SpriteRow[8];
    int evaluationSprite, evaluationByte, selectedCount, activeCount, fetchedCount;
    byte evaluationLatch;
    public ReadOnlyMemory<byte> SecondaryOam => secondaryOam;
    public int SelectedSpriteCount => selectedCount;
    public int ActiveSpriteCount => activeCount;
    struct SpriteRow
    {
        public int Id, X, Attributes, Pattern;
        public byte Low, High;
    }
    void BeginSpriteLine()
    {
        activeCount = fetchedCount;
        fetchedSprites.CopyTo(activeSprites, 0);
    }
    void ClockSprites()
    {
        // Hardware performs evaluation for the following line. Pre-render produces no visible row-zero sprites.
        if (Dot is >= 1 and <= 64)
        {
            if ((Dot & 1) == 0) secondaryOam[Dot / 2 - 1] = 255;
        }
        if (Dot == 65) { evaluationSprite = 0; evaluationByte = 0; selectedCount = 0; Array.Fill(secondaryIds, -1); }
        if (Dot is >= 65 and <= 256 && evaluationSprite < 64)
        {
            if ((Dot & 1) != 0) evaluationLatch = Oam[evaluationSprite * 4 + evaluationByte];
            else EvaluateSpriteByte();
        }
        if (Dot is >= 257 and <= 320) FetchSpriteByte();
    }
    void EvaluateSpriteByte()
    {
        if (evaluationByte == 0)
        {
            int row = (Scanline == 261 ? -1 : Scanline) - evaluationLatch;
            int height = (control & 32) != 0 ? 16 : 8;
            if (row < 0 || row >= height) { evaluationSprite++; return; }
            if (selectedCount == 8)
            {
                // Deliberately simple overflow: scan Y only, without the hardware diagonal n/m bug.
                status |= 32; evaluationSprite++; return;
            }
            secondaryIds[selectedCount] = evaluationSprite;
        }
        secondaryOam[selectedCount * 4 + evaluationByte] = evaluationLatch;
        if (++evaluationByte == 4) { evaluationByte = 0; selectedCount++; evaluationSprite++; }
    }
    void FetchSpriteByte()
    {
        int slot = (Dot - 257) / 8, phase = (Dot - 257) & 7;
        if (Dot == 257) fetchedCount = selectedCount;
        if (phase == 0)
        {
            int o = slot * 4;
            int tile = secondaryOam[o + 1], attributes = secondaryOam[o + 2];
            int row = (Scanline == 261 ? -1 : Scanline) - secondaryOam[o];
            int height = (control & 32) != 0 ? 16 : 8;
            if ((attributes & 128) != 0) row = height - 1 - row;
            int pattern = height == 16
                ? (tile & 1) * 0x1000 + ((tile & 0xfe) + ((row >> 3) & 1)) * 16
                : ((control & 8) != 0 ? 0x1000 : 0) + tile * 16;
            fetchedSprites[slot] = new SpriteRow { Id = secondaryIds[slot], X = secondaryOam[o + 3], Attributes = attributes, Pattern = pattern + (row & 7) };
        }
        // Dummy nametable reads occupy the otherwise idle phases; mapper-visible bus timing is not exposed yet.
        if (phase is 0 or 2) _ = ReadMemory(0x2000 | (address & 0x0fff));
        if (phase == 4) fetchedSprites[slot].Low = ReadMemory(fetchedSprites[slot].Pattern);
        if (phase == 6) fetchedSprites[slot].High = ReadMemory(fetchedSprites[slot].Pattern + 8);
    }
}
