namespace Fake64.Tests;

[TestClass]
public sealed class MOS6526Tests
{
    private MOS6526 _cia = null!;
    private int _interruptCalls;

    [TestInitialize]
    public void Setup()
    {
        _interruptCalls = 0;
        _cia = new MOS6526(null!, () => _interruptCalls++, _ => 0xFF, (_, _) => { });
    }

    // Un ciclo di clock, nello stesso ordine usato dalla Board
    private void Tick(int cycles = 1)
    {
        for (int i = 0; i < cycles; i++)
        {
            _cia.ProcessSignals(0);
            _cia.Clock(0);
        }
    }

    private void StartTimerA(ushort latch, byte controlA)
    {
        _cia.Address(0x04, (byte)latch);
        _cia.Address(0x05, (byte)(latch >> 8));
        _cia.Address(0x0E, controlA);
    }

    private int CountTimerAUnderflows(int cycles)
    {
        int underflows = 0;
        for (int i = 0; i < cycles; i++)
        {
            Tick();
            if ((_cia.Address(0x0D) & 0x01) != 0) underflows++; // la lettura azzera i flag
        }
        return underflows;
    }

    [TestMethod]
    public void Reset_ShouldStopTimersAndClearInterrupts()
    {
        Assert.AreEqual(0, (int)_cia.Address(0x0E));
        Assert.AreEqual(0, (int)_cia.Address(0x0F));
        Assert.AreEqual(0, (int)_cia.Address(0x0D));
        Assert.IsFalse(_cia.IrqAsserted);
    }

    [TestMethod]
    public void TimerA_ShouldCountDown()
    {
        StartTimerA(0x0010, 0x11);

        Tick(3);

        Assert.AreEqual(0x0D, (int)_cia.Address(0x04));
        Assert.AreEqual(0x00, (int)_cia.Address(0x05));
    }

    [TestMethod]
    public void TimerA_ContinuousMode_ShouldKeepRunningAfterUnderflow()
    {
        StartTimerA(0x0004, 0x11); // start + force load, bit 3 = 0 -> continuo

        int underflows = CountTimerAUnderflows(40);

        Assert.IsTrue(underflows >= 8, $"underflow: {underflows}");
        Assert.AreEqual(0x01, _cia.Address(0x0E) & 0x01); // ancora in esecuzione
    }

    [TestMethod]
    public void TimerA_OneShotMode_ShouldStopAfterFirstUnderflow()
    {
        StartTimerA(0x0004, 0x19); // start + force load + one-shot (bit 3 = 1)

        int underflows = CountTimerAUnderflows(40);

        Assert.AreEqual(1, underflows);
        Assert.AreEqual(0x00, _cia.Address(0x0E) & 0x01); // start bit azzerato
    }

    [TestMethod]
    public void TimerA_Underflow_ShouldAssertIrqOnlyWhenEnabled()
    {
        StartTimerA(0x0002, 0x11);
        Tick(10);
        Assert.IsFalse(_cia.IrqAsserted);

        _cia.Address(0x0D, 0x81); // abilita Timer A
        Assert.IsTrue(_cia.IrqAsserted);
    }

    [TestMethod]
    public void ReadingIcr_ShouldClearFlagsAndReleaseIrq()
    {
        _cia.Address(0x0D, 0x81);
        StartTimerA(0x0002, 0x11);
        Tick(3);
        Assert.IsTrue(_cia.IrqAsserted);

        byte icr = _cia.Address(0x0D);

        Assert.AreEqual(0x81, icr & 0x81);
        Assert.IsFalse(_cia.IrqAsserted);
        Assert.AreEqual(0, (int)_cia.Address(0x0D));
    }

    [TestMethod]
    public void InterruptHandler_ShouldBeInvokedOncePerAssertion()
    {
        // Sulla CIA2 l'handler genera un NMI, che è edge-triggered
        _cia.Address(0x0D, 0x81);
        StartTimerA(0x0002, 0x11);

        Tick(20); // più underflow senza leggere l'ICR
        Assert.AreEqual(1, _interruptCalls);

        _cia.Address(0x0D); // acknowledge
        Tick(5);
        Assert.AreEqual(2, _interruptCalls);
    }

    [TestMethod]
    public void ForceLoad_ShouldLoadLatchWhileStopped_AndNotBeStored()
    {
        _cia.Address(0x04, 0x34);
        _cia.Address(0x05, 0x12);

        _cia.Address(0x0E, 0x10); // force load a timer fermo

        Assert.AreEqual(0x34, (int)_cia.Address(0x04));
        Assert.AreEqual(0x12, (int)_cia.Address(0x05));
        Assert.AreEqual(0x00, (int)_cia.Address(0x0E)); // il bit 4 è uno strobe
    }

    [TestMethod]
    public void TimerB_ShouldCountTimerAUnderflows()
    {
        _cia.Address(0x06, 0x10);
        _cia.Address(0x07, 0x00);
        _cia.Address(0x0F, 0x51); // start + force load + conta gli underflow di Timer A
        StartTimerA(0x0004, 0x11);

        int underflowsA = CountTimerAUnderflows(20);

        Assert.AreEqual(0x10 - underflowsA, (int)_cia.Address(0x06));
    }
}
