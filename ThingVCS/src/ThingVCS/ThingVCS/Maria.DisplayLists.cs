using ThingRetroEmu.Chips;
namespace ThingVCS;

public sealed partial class Maria
{
    public const int DmaClockLimit = 426;
    public const int DmaStartDot = 28;
    public const int DliOverheadClocks = 17;
    public long DliOverheadClampedLines { get; private set; }
    public long DliOverheadMasterClocks { get; private set; }
    public long LastDliMasterClock { get; private set; } = -1;
    void PulseDli()
    {
        nmiClocks = 8; DisplayListInterrupts++; LastDliMasterClock = MasterClocks;
    }
    public Action<MariaDmaTrace>? DmaTrace { get; set; }
    readonly byte[][] lineBuffers = [new byte[160], new byte[160]];
    int writeBuffer;
    byte[] lineRam => lineBuffers[writeBuffer];
    byte[] outputLineRam => lineBuffers[writeBuffer ^ 1];
    ushort dll, dl;
    int zoneOffset;
    byte holey;
    int nmiClocks;
    IEnumerator<int>? dma;
    int delay;
    bool dmaHold, releasePending, dliOverheadActive;
    long dmaStart;
    public long MasterClocks { get; private set; }
    public Func<bool>? BusBoundaryInput { get; set; }
    bool AtBusBoundary => BusBoundaryInput?.Invoke() ?? ((MasterClocks & 3) == 0);
    public bool DmaBusy => dmaHold;
    public int LastDmaClocks { get; private set; }
    public bool Nmi => nmiClocks > 0;
    public long DisplayListInterrupts { get; private set; }
    public long SuppressedGraphicsFetches { get; private set; }
    bool advanceZone, writeMode;
    public IBus? Bus { get; set; }
    public long GraphicsReads { get; private set; }
    public long UnsupportedLines { get; private set; }
    public long TruncatedLines { get; private set; }
    bool IsHoley(int address)
    {
        ushort busAddress = unchecked((ushort)address);
        return ((holey & 2) != 0 && (busAddress & 0x9000) == 0x9000)
            || ((holey & 1) != 0 && (busAddress & 0x8800) == 0x8800);
    }
    byte Fetch(int address)
    {
        GraphicsReads++;
        return Bus!.Read(unchecked((ushort)address));
    }
    void WriteGraphicsByte(byte data, int palette, ref int position)
    {
        if (writeMode)
        {
            WriteCell((palette & 0x10) | (data & 0x0c) | (data >> 6), ref position);
            WriteCell((palette & 0x10) | ((data & 3) << 2) | ((data & 0x30) >> 4), ref position);
            return;
        }
        for (int pixel = 0; pixel < 4; pixel++)
            WriteCell(palette | ((data >> (6 - pixel * 2)) & 3), ref position);
    }
    void WriteCell(int cell, ref int position)
    {
        if (position < 160 && ((cell & 3) != 0 || (Control & 4) != 0))
            lineRam[position] = (byte)cell;
        position = (position + 1) & 255;
    }
    void CancelDma()
    {
        dma?.Dispose(); dma = null; delay = 0;
        dmaHold = releasePending = dliOverheadActive = false;
    }
    void ResetDisplayLists()
    {
        CancelDma(); MasterClocks = 0;
        foreach (var buffer in lineBuffers) Array.Clear(buffer);
        writeBuffer = 0; dll = dl = 0; zoneOffset = 0; advanceZone = writeMode = false;
        GraphicsReads = UnsupportedLines = TruncatedLines = SuppressedGraphicsFetches = 0;
        holey = 0; nmiClocks = 0; LastDmaClocks = 0; DisplayListInterrupts = 0;
        DliOverheadClampedLines = DliOverheadMasterClocks = 0; LastDliMasterClock = -1;
    }
    void BeginLine()
    {
        CancelDma();
        Array.Clear(lineRam); LastDmaClocks = 0;
        if (Scanline == FirstVisibleLine) advanceZone = true;
    }
    void StepDma()
    {
        if (Bus is null) { CancelDma(); return; }
        if (!Vblank && Dot >= DmaStartDot && !dmaHold && !releasePending && LastDmaClocks == 0 &&
            AtBusBoundary && (Control & 0x60) == 0x40)
        {
            dmaHold = true; dmaStart = MasterClocks;
            dma = RunDma().GetEnumerator();
            if (dma.MoveNext()) delay = dma.Current;
            return;
        }
        if (dmaHold) LastDmaClocks = (int)(MasterClocks - dmaStart);
        if (dma is not null && dliOverheadActive) DliOverheadMasterClocks++;
        if (dma is not null && --delay <= 0)
        {
            if (dma.MoveNext()) delay = dma.Current;
            else
            {
                dma.Dispose(); dma = null; releasePending = true;
            }
        }
        if (releasePending && AtBusBoundary)
        {
            LastDmaClocks = (int)(MasterClocks - dmaStart);
            dmaHold = releasePending = false;
        }
    }
    // Each yield delays the NEXT bus operation. No prerecorded reads or replayed RAM values.
    // Header bytes cost two master clocks, graphics/map bytes three. Exact electrical phases remain pending.
    IEnumerable<int> RunDma()
    {
        if (Scanline == FirstVisibleLine) dll = DisplayListPointer;
        // Keep the final continuation within this line even after phase-aligned startup.
        int limit = Math.Min(DmaClockLimit, DotsPerLine - Dot - 1);
        bool initialLine = Scanline == FirstVisibleLine;
        bool initialDli = false;
        bool loadZone = advanceZone;
        if (loadZone)
        {
            yield return 2; byte zone = Fetch(dll);
            yield return 2; byte high = Fetch(dll + 1);
            yield return 2; byte low = Fetch(dll + 2);
            zoneOffset = zone & 15; dl = (ushort)((high << 8) | low); holey = (byte)((zone >> 5) & 3);
            initialDli = (zone & 0x80) != 0; // Deliver only after the initial line's rendering.
            advanceZone = false;
            yield return 10; // DLL acquisition is included in the 16-clock startup.
        }
        else yield return 16;
        List<MariaDmaObject>? tracedObjects = DmaTrace is null ? null : [];
        int pointer = dl, budget = 16;
        bool terminated = false, exhausted = false;
        for (int objects = 0; objects < 64; objects++)
        {
            if (budget + 2 > limit) { exhausted = true; break; }
            yield return 2; byte descriptor = Fetch(pointer + 1);
            if ((descriptor & 0x5f) == 0) { budget += 2; terminated = true; break; }
            bool extended = (descriptor & 31) == 0;
            int headerCost = extended ? 10 : 8;
            if (budget + headerCost > limit) { budget += 2; exhausted = true; break; }
            budget += headerCost;
            if (extended) writeMode = (descriptor & 0x80) != 0;
            yield return 2; byte graphicsLow = Fetch(pointer);
            yield return 2; int graphics = graphicsLow | (Fetch(pointer + 2) << 8);
            byte paletteWidth = descriptor;
            if (extended) { yield return 2; paletteWidth = Fetch(pointer + 3); }
            yield return 2; int position = Fetch(pointer + (extended ? 4 : 3));
            int palette = (paletteWidth >> 5) << 2, width = 32 - (paletteWidth & 31);
            bool indirect = extended && (descriptor & 0x20) != 0;
            tracedObjects?.Add(new((ushort)pointer, (ushort)graphics, width, position, palette, indirect, writeMode));
            pointer = (pointer + (extended ? 5 : 4)) & 65535;
            for (int index = 0; index < width; index++)
            {
                int address = graphics + index + (zoneOffset << 8);
                if (indirect)
                {
                    if (budget + 3 > limit) { exhausted = true; break; }
                    budget += 3;
                    yield return 3; byte character = Fetch(graphics + index);
                    address = (registers[20] << 8) + character + (zoneOffset << 8);
                }
                bool pair = indirect && (Control & 0x10) != 0;
                if (IsHoley(address))
                {
                    SuppressedGraphicsFetches += pair ? 2 : 1;
                    continue;
                }
                int cost = pair ? 6 : 3;
                if (budget + cost > limit) { exhausted = true; break; }
                budget += cost;
                yield return 3; WriteGraphicsByte(Fetch(address), palette, ref position);
                if (pair) { yield return 3; WriteGraphicsByte(Fetch(address + 1), palette, ref position); }
            }
            if (exhausted) break;
        }
        if (!terminated) TruncatedLines++;
        if (exhausted || !terminated || budget >= limit) WaitSync = true;
        if (initialDli)
        {
            PulseDli();
            int overhead = Math.Min(DliOverheadClocks, limit - budget);
            if (overhead < DliOverheadClocks) { DliOverheadClampedLines++; WaitSync = true; }
            budget += overhead;
            if (overhead > 0)
            {
                dliOverheadActive = true;
                yield return overhead;
                dliOverheadActive = false;
            }
        }
        bool prefetched = false;
        byte nextZone = 0;
        ushort nextDl = 0;
        if (!initialLine && zoneOffset == 0)
        {
            // Six post-render clocks load the next DLL entry, not a speculative peek.
            bool nextVisible = Scanline < FirstVisibleLine + Timing.ActiveLines - 1;
            if (nextVisible && budget + 6 <= limit)
            {
                ushort nextDll = unchecked((ushort)(dll + 3));
                budget += 6;
                yield return 2; nextZone = Fetch(nextDll);
                yield return 2; byte high = Fetch(nextDll + 1);
                yield return 2; byte low = Fetch(nextDll + 2);
                nextDl = (ushort)((high << 8) | low); prefetched = true;
                if ((nextZone & 0x80) != 0)
                {
                    PulseDli();
                    int overhead = Math.Min(DliOverheadClocks, limit - budget);
                    if (overhead < DliOverheadClocks) { DliOverheadClampedLines++; WaitSync = true; }
                    budget += overhead;
                    if (overhead > 0)
                    {
                        dliOverheadActive = true;
                        yield return overhead;
                        dliOverheadActive = false;
                    }
                }
            }
            else
            {
                int shutdown = Math.Min(6, limit - budget);
                if (shutdown > 0) { budget += shutdown; yield return shutdown; }
            }
        }
        if (exhausted || !terminated || budget >= limit) WaitSync = true;
        if (tracedObjects is not null)
            DmaTrace?.Invoke(new(Frames, Scanline, dll, dl, zoneOffset, holey, budget, terminated, exhausted, tracedObjects.AsReadOnly()));
        // Bootstrap line initializes the zone; regular countdown starts next line.
        // Matches the frame_scanline > 16 guard in the archived MAME startdma.
        if (initialLine) yield break;
        if (zoneOffset == 0)
        {
            dll = unchecked((ushort)(dll + 3)); advanceZone = !prefetched;
            if (prefetched)
            {
                zoneOffset = nextZone & 15; dl = nextDl; holey = (byte)((nextZone >> 5) & 3);
            }
        }
        else zoneOffset--;
    }
}
