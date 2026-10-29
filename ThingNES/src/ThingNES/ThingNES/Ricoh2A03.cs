using ThingMOS;

namespace ThingNES;

// CPU portion only: APU is not implemented yet.
public sealed class Ricoh2A03 : MOS6502
{
    protected override bool SupportsDecimalArithmetic => false;
    // Deterministic $AB model used by Blargg's NES instruction tests. Real silicon is unstable.
    protected override byte LxaMagic => 0xFF;
}
