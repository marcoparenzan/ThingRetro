namespace ThingVCS;

/// <summary>Observations of existing DMA fetches; collecting a trace does not read the bus.</summary>
public sealed record MariaDmaObject(ushort Header, ushort Graphics, int Width, int Position, int Palette, bool Indirect, bool WriteMode);
public sealed record MariaDmaTrace(long Frame, int Scanline, ushort Dll, ushort DisplayList, int ZoneOffset, byte Holey, int Budget, bool Terminated, bool Exhausted, IReadOnlyList<MariaDmaObject> Objects);
