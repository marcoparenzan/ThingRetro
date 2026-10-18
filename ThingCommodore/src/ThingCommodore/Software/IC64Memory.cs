namespace ThingCommodore.Software;

/// <summary>
/// A machine with the memory map and the KERNAL of a C64 (a Thing64, or a Thing65 in C64 mode): what the loaders of this namespace need from it is the 64 KB of RAM,
/// which they write under the ROMs too, like a DMA
/// </summary>
public interface IC64Memory
{
    Ram64KbChip Ram { get; }
}
