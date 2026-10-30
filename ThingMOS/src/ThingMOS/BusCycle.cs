namespace ThingMOS;

/// <summary>Address and direction of the next NMOS 6502 bus cycle, without bus traffic.</summary>
public readonly record struct BusCycle(ushort Address, bool IsWrite);
