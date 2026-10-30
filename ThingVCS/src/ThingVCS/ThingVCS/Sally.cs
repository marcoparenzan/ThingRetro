using ThingMOS;
namespace ThingVCS;

public interface ISallyPinout : I6502Pinout
{
    Func<bool> HaltInput { get; set; } // True = asserted HALT, board owns the bus.
    BusCycle? NextBusCycle { get; }
    void SamplePins();
}

/// <summary>SALLY HALT adapter with side-effect-free bus preview and pin sampling; board drives logical PHI1/PHI2 phases. Electrical timing remains unvalidated.</summary>
public sealed class Sally : MOS6502, ISallyPinout
{
    public Func<bool> HaltInput { get; set; } = () => false;
    public BusCycle? NextBusCycle => PreviewBusCycle();
    public void SamplePins() => SampleInputPins();
    public Sally() => AecInput = () => !HaltInput();
    // The existing core samples NMI before AEC blocks execution: pending NMI survives HALT.
}
