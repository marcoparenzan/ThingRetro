namespace Fake64.Tests;

[TestClass]
public sealed class MOS6526Tests
{
    // La CIA da sola, fuori da qualunque scheda: si usa solo attraverso la sua piedinatura
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
            if ((_cia.Read(0x0D) & 0x01) != 0) underflows++; // la lettura azzera i flag
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
        StartTimerA(0x0004, 0x11); // start + force load, bit 3 = 0 -> continuo

        int underflows = CountTimerAUnderflows(40);

        Assert.IsTrue(underflows >= 8, $"underflow: {underflows}");
        Assert.AreEqual(0x01, _cia.Read(0x0E) & 0x01); // ancora in esecuzione
    }

    [TestMethod]
    public void TimerA_OneShotMode_ShouldStopAfterFirstUnderflow()
    {
        StartTimerA(0x0004, 0x19); // start + force load + one-shot (bit 3 = 1)

        int underflows = CountTimerAUnderflows(40);

        Assert.AreEqual(1, underflows);
        Assert.AreEqual(0x00, _cia.Read(0x0E) & 0x01); // start bit azzerato
    }

    [TestMethod]
    public void TimerA_Underflow_ShouldAssertIrqOnlyWhenEnabled()
    {
        StartTimerA(0x0002, 0x11);
        Tick(10);
        Assert.IsFalse(_cia.Irq);

        _cia.Write(0x0D, 0x81); // abilita Timer A
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

        Tick(20); // più underflow senza leggere l'ICR: la linea resta attiva, non pulsa
        Assert.IsTrue(_cia.Irq);

        _cia.Read(0x0D); // acknowledge
        Assert.IsFalse(_cia.Irq);
        Tick(5);
        Assert.IsTrue(_cia.Irq);
    }

    [TestMethod]
    public void Ports_ShouldDriveOutputsAndReadExternalInputs()
    {
        _cia.PortA.External = () => 0xFA;  // l'esterno tira a massa PA0 e PA2
        _cia.Write(0x02, 0xF0);            // PA4-PA7 in uscita
        _cia.Write(0x00, 0xA5);

        Assert.AreEqual(0xA0, (int)_cia.PortA.Output);
        Assert.AreEqual(0xAA, (int)_cia.Read(0x00)); // uscite dal latch, ingressi con pull-up, AND con l'esterno
    }

    [TestMethod]
    public void Ports_ExternalDeviceShouldPullOutputsLow()
    {
        // Le uscite NMOS hanno un pull-up debole: un contatto a massa vince (così si legge un joystick)
        _cia.Write(0x02, 0xFF);
        _cia.Write(0x00, 0xFF);
        _cia.PortA.External = () => 0xEF;  // fuoco premuto: PA4 a massa

        Assert.AreEqual(0xEF, (int)_cia.Read(0x00));
        Assert.AreEqual(0xFF, (int)_cia.PortA.Output); // il chip continua a pilotare 1
    }

    [TestMethod]
    public void ForceLoad_ShouldLoadLatchWhileStopped_AndNotBeStored()
    {
        _cia.Write(0x04, 0x34);
        _cia.Write(0x05, 0x12);

        _cia.Write(0x0E, 0x10); // force load a timer fermo

        Assert.AreEqual(0x34, (int)_cia.Read(0x04));
        Assert.AreEqual(0x12, (int)_cia.Read(0x05));
        Assert.AreEqual(0x00, (int)_cia.Read(0x0E)); // il bit 4 è uno strobe
    }

    [TestMethod]
    public void TimerB_ShouldCountTimerAUnderflows()
    {
        _cia.Write(0x06, 0x10);
        _cia.Write(0x07, 0x00);
        _cia.Write(0x0F, 0x51); // start + force load + conta gli underflow di Timer A
        StartTimerA(0x0004, 0x11);

        int underflowsA = CountTimerAUnderflows(20);

        Assert.AreEqual(0x10 - underflowsA, (int)_cia.Read(0x06));
    }
}
