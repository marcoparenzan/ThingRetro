namespace ThingRetroEmu.Tests;

[TestClass]
public sealed class MOS6526Tests
{
    // The CIA alone, outside any board: it is used only through its pinout
    private MOS6526 _cia = null!;

    [TestInitialize]
    public void Setup()
    {
        _cia = new MOS6526();
    }

    private void Tick(int cycles = 1)
    {
        for (int i = 0; i < cycles; i++) _cia.Tick();
    }

    private void StartTimerA(ushort latch, byte controlA)
    {
        _cia.Write(0x04, (byte)latch);
        _cia.Write(0x05, (byte)(latch >> 8));
        _cia.Write(0x0E, controlA);
    }

    private int CountTimerAUnderflows(int cycles)
    {
        int underflows = 0;
        for (int i = 0; i < cycles; i++)
        {
            Tick();
            if ((_cia.Read(0x0D) & 0x01) != 0) underflows++; // reading clears the flags
        }
        return underflows;
    }

    [TestMethod]
    public void Reset_ShouldStopTimersAndClearInterrupts()
    {
        Assert.AreEqual(0, (int)_cia.Read(0x0E));
        Assert.AreEqual(0, (int)_cia.Read(0x0F));
        Assert.AreEqual(0, (int)_cia.Read(0x0D));
        Assert.IsFalse(_cia.Irq);
    }

    [TestMethod]
    public void TimerA_ShouldCountDown()
    {
        StartTimerA(0x0010, 0x11);

        Tick(3);

        Assert.AreEqual(0x0D, (int)_cia.Read(0x04));
        Assert.AreEqual(0x00, (int)_cia.Read(0x05));
    }

    [TestMethod]
    public void TimerA_ContinuousMode_ShouldKeepRunningAfterUnderflow()
    {
        StartTimerA(0x0004, 0x11); // start + force load, bit 3 = 0 -> continuous

        int underflows = CountTimerAUnderflows(40);

        Assert.IsTrue(underflows >= 8, $"underflow: {underflows}");
        Assert.AreEqual(0x01, _cia.Read(0x0E) & 0x01); // still running
    }

    [TestMethod]
    public void TimerA_OneShotMode_ShouldStopAfterFirstUnderflow()
    {
        StartTimerA(0x0004, 0x19); // start + force load + one-shot (bit 3 = 1)

        int underflows = CountTimerAUnderflows(40);

        Assert.AreEqual(1, underflows);
        Assert.AreEqual(0x00, _cia.Read(0x0E) & 0x01); // start bit cleared
    }

    [TestMethod]
    public void TimerA_Underflow_ShouldAssertIrqOnlyWhenEnabled()
    {
        StartTimerA(0x0002, 0x11);
        Tick(10);
        Assert.IsFalse(_cia.Irq);

        _cia.Write(0x0D, 0x81); // enable Timer A
        Assert.IsTrue(_cia.Irq);
    }

    [TestMethod]
    public void ReadingIcr_ShouldClearFlagsAndReleaseIrq()
    {
        _cia.Write(0x0D, 0x81);
        StartTimerA(0x0002, 0x11);
        Tick(3);
        Assert.IsTrue(_cia.Irq);

        byte icr = _cia.Read(0x0D);

        Assert.AreEqual(0x81, icr & 0x81);
        Assert.IsFalse(_cia.Irq);
        Assert.AreEqual(0, (int)_cia.Read(0x0D));
    }

    [TestMethod]
    public void Irq_ShouldStayAssertedUntilIcrIsRead()
    {
        _cia.Write(0x0D, 0x81);
        StartTimerA(0x0002, 0x11);

        Tick(20); // several underflows without reading the ICR: the line stays active, it does not pulse
        Assert.IsTrue(_cia.Irq);

        _cia.Read(0x0D); // acknowledge
        Assert.IsFalse(_cia.Irq);
        Tick(5);
        Assert.IsTrue(_cia.Irq);
    }

    [TestMethod]
    public void Ports_ShouldDriveOutputsAndReadExternalInputs()
    {
        _cia.PortA.External = () => 0xFA;  // the outside pulls PA0 and PA2 to ground
        _cia.Write(0x02, 0xF0);            // PA4-PA7 as outputs
        _cia.Write(0x00, 0xA5);

        Assert.AreEqual(0xA0, (int)_cia.PortA.Output);
        Assert.AreEqual(0xAA, (int)_cia.Read(0x00)); // outputs from the latch, inputs with pull-ups, AND with the outside
    }

    [TestMethod]
    public void Ports_ExternalDeviceShouldPullOutputsLow()
    {
        // NMOS outputs have a weak pull-up: a contact to ground wins (that is how a joystick is read)
        _cia.Write(0x02, 0xFF);
        _cia.Write(0x00, 0xFF);
        _cia.PortA.External = () => 0xEF;  // fire pressed: PA4 to ground

        Assert.AreEqual(0xEF, (int)_cia.Read(0x00));
        Assert.AreEqual(0xFF, (int)_cia.PortA.Output); // the chip keeps driving 1
    }

    [TestMethod]
    public void ForceLoad_ShouldLoadLatchWhileStopped_AndNotBeStored()
    {
        _cia.Write(0x04, 0x34);
        _cia.Write(0x05, 0x12);

        _cia.Write(0x0E, 0x10); // force load with the timer stopped

        Assert.AreEqual(0x34, (int)_cia.Read(0x04));
        Assert.AreEqual(0x12, (int)_cia.Read(0x05));
        Assert.AreEqual(0x00, (int)_cia.Read(0x0E)); // bit 4 is a strobe
    }

    [TestMethod]
    public void TimerB_ShouldCountTimerAUnderflows()
    {
        _cia.Write(0x06, 0x10);
        _cia.Write(0x07, 0x00);
        _cia.Write(0x0F, 0x51); // start + force load + count the underflows of Timer A
        StartTimerA(0x0004, 0x11);

        int underflowsA = CountTimerAUnderflows(20);

        Assert.AreEqual(0x10 - underflowsA, (int)_cia.Read(0x06));
    }
}
