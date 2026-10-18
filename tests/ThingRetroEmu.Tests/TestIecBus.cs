namespace ThingRetroEmu.Tests;

[TestClass]
public sealed class IecBusTests
{
    [TestMethod]
    public void Line_ShouldBeHighUntilADevicePullsItDown()
    {
        var bus = new IecBus();
        bool computer = false, drive = false;
        bus.Data.Connect(() => computer);
        bus.Data.Connect(() => drive);
        Assert.IsFalse(bus.Data.IsLow);

        drive = true;
        Assert.IsTrue(bus.Data.IsLow);
        computer = true;
        drive = false;
        Assert.IsTrue(bus.Data.IsLow, "wired AND: one is enough");
        computer = false;
        Assert.IsFalse(bus.Data.IsLow);
    }

    [TestMethod]
    public void Lines_ShouldBeIndependent()
    {
        var bus = new IecBus();
        bus.Atn.Connect(() => true);

        Assert.IsTrue(bus.Atn.IsLow);
        Assert.IsFalse(bus.Clk.IsLow);
        Assert.IsFalse(bus.Data.IsLow);
    }
}
