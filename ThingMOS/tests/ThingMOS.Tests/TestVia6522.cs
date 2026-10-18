namespace ThingMOS.Tests;

[TestClass]
public sealed class Via6522Tests
{
    static Via6522 NewVia()
    {
        var via = new Via6522();
        via.Reset();
        return via;
    }

    static void Run(Via6522 via, int cycles)
    {
        for (int i = 0; i < cycles; i++) via.Tick();
    }

    [TestMethod]
    public void Ports_ShouldMixOutputsAndPulledUpInputs()
    {
        var via = NewVia();
        via.PortA.External = () => 0b1010_1111;
        via.Write(3, 0x0F);          // PA0-PA3 outputs
        via.Write(1, 0x05);

        Assert.AreEqual(0b1010_0101, via.Read(1));
        Assert.AreEqual(0x0F, via.Read(3));
    }

    [TestMethod]
    public void Timer1_OneShot_ShouldInterruptOnceAfterNPlusOneAndAHalfCycles()
    {
        var via = NewVia();
        via.Write(14, 0xC0);         // enable the T1 interrupt
        via.Write(4, 10);
        via.Write(5, 0);

        Run(via, 10);
        Assert.IsFalse(via.Irq, "the counter has reached 0 but not underflowed");
        Run(via, 1);
        Assert.IsTrue(via.Irq);
        Assert.AreEqual(0xC0, via.Read(13) & 0xC0);

        via.Read(4);                 // reading the low counter clears the flag
        Assert.IsFalse(via.Irq);
        Run(via, 100);
        Assert.IsFalse(via.Irq, "a one shot interrupts once");
    }

    [TestMethod]
    public void Timer1_FreeRun_ShouldRepeatEveryNPlusTwoCycles()
    {
        var via = NewVia();
        via.Write(11, 0x40);         // ACR: T1 free run
        via.Write(14, 0xC0);
        via.Write(4, 10);
        via.Write(5, 0);

        var fires = new List<int>();
        for (int cycle = 1; cycle <= 60; cycle++)
        {
            via.Tick();
            if (via.Irq)
            {
                fires.Add(cycle);
                via.Write(13, 0x40);
            }
        }

        CollectionAssert.AreEqual(new[] { 11, 23, 35, 47, 59 }, fires);
    }

    [TestMethod]
    public void Timer2_ShouldInterruptOnceAndBeClearedByReadingTheLowByte()
    {
        var via = NewVia();
        via.Write(14, 0xA0);
        via.Write(8, 3);
        via.Write(9, 0);

        Run(via, 4);

        Assert.IsTrue(via.Irq);
        via.Read(8);
        Assert.IsFalse(via.Irq);
    }

    [TestMethod]
    public void WritingTheHighLatch_ShouldClearTheFlagButNotRestartTheTimer()
    {
        var via = NewVia();
        via.Write(14, 0xC0);
        via.Write(4, 2);
        via.Write(5, 0);
        Run(via, 5);
        Assert.IsTrue(via.Irq);

        via.Write(7, 0x10);

        Assert.IsFalse(via.Irq);
    }

    [TestMethod]
    public void Ca1_ShouldInterruptOnTheEdgeSelectedByPcr()
    {
        bool line = true;
        var via = NewVia();
        via.Ca1Input = () => line;
        via.Reset();
        via.Write(14, 0x82);          // CA1 interrupt enabled
        via.Write(12, 0x00);          // falling edge

        line = false;
        via.Tick();

        Assert.IsTrue(via.Irq);
        via.Read(1);                  // reading port A clears it
        Assert.IsFalse(via.Irq);
        line = true;
        via.Tick();
        Assert.IsFalse(via.Irq, "the rising edge is not the one selected");

        via.Write(12, 0x01);          // rising edge
        line = false;
        via.Tick();
        line = true;
        via.Tick();
        Assert.IsTrue(via.Irq);
    }

    [TestMethod]
    public void InterruptEnable_ShouldSetAndClearBitsWithBit7()
    {
        var via = NewVia();

        via.Write(14, 0xC1);
        Assert.AreEqual(0xC1, via.Read(14));
        via.Write(14, 0x40);          // bit 7 clear: clears T1
        Assert.AreEqual(0x81, via.Read(14));
    }

    [TestMethod]
    public void WritingIfr_ShouldClearTheFlagsWrittenWithOne()
    {
        var via = NewVia();
        via.Write(14, 0xE0);
        via.Write(4, 1); via.Write(5, 0);
        via.Write(8, 1); via.Write(9, 0);
        Run(via, 5);
        Assert.AreEqual(0xE0, via.Read(13));

        via.Write(13, 0x20);

        Assert.AreEqual(0xC0, via.Read(13));
    }
}
