namespace ThingCommodore.Tests;

// The DOS of the 1541 talking to a computer made for the test: the bytes of the IEC protocol are sent by hand on the
// cable, one microsecond (cycle) at a time
[TestClass]
public sealed class IecProtocolTests
{
    sealed class Computer
    {
        public readonly IecBus Cable = new();
        public readonly Board1541 Drive;
        public bool Atn, Clk, Data;     // true = this side pulls the line down
        public string Failure = "";
        public readonly List<string> Log = [];
        int clock, last = -1;

        void Tick()
        {
            Drive.Tick();
            clock++;
            int state = (Cable.Atn.IsLow ? 4 : 0) | (Cable.Clk.IsLow ? 2 : 0) | (Cable.Data.IsLow ? 1 : 0);
            if (state != last) Log.Add($"{clock}: ATN={state >> 2 & 1} CLK={state >> 1 & 1} DATA={state & 1}");
            last = state;
        }

        public Computer(Board1541 drive)
        {
            Drive = drive;
            Cable.Atn.Connect(() => Atn);
            Cable.Clk.Connect(() => Clk);
            Cable.Data.Connect(() => Data);
            drive.Plug(Cable);
        }

        public void Delay(int cycles)
        {
            for (int i = 0; i < cycles; i++) Tick();
        }

        // Waits until the condition holds; false after the time limit
        public bool WaitFor(Func<bool> condition, int limit)
        {
            for (int i = 0; i < limit; i++)
            {
                if (condition()) return true;
                Tick();
            }
            return condition();
        }

        // One byte as a talker: CLK is down since the start, the listener is ready when DATA is released
        public bool SendByte(byte value)
        {
            Clk = false;                                    // ready to send
            if (!WaitFor(() => !Cable.Data.IsLow, 20_000))
            {
                Failure = "the listener never got ready";
                return false;
            }
            Delay(60);
            Clk = true;
            for (int bit = 0; bit < 8; bit++)
            {
                Data = (value & 1 << bit) == 0;             // a 1 is the line released
                Delay(40);
                Clk = false;                                // the bit is valid
                Delay(80);
                Clk = true;
                Delay(40);                                  // the DOS needs time to see the clock low
            }
            Data = false;
            bool acknowledged = WaitFor(() => Cable.Data.IsLow, 2_000);  // the frame handshake: the listener pulls DATA down
            if (!acknowledged) Failure = "no frame handshake";
            return acknowledged;
        }
    }

    static Board1541 NewDrive(int device = 8)
    {
        var file = Path.Combine(AppContext.BaseDirectory, "roms", "c-drive-1541.rom");
        if (!File.Exists(file)) Assert.Inconclusive($"ROM missing: {file}");
        return new Board1541(File.ReadAllBytes(file)) { DeviceNumber = device };
    }

    [TestMethod]
    [Timeout(60_000)]
    public void Attention_ShouldMakeTheDriveAnswerWithDataDown()
    {
        var computer = new Computer(NewDrive());
        computer.Delay(2_000_000);               // reset and self test

        computer.Atn = true;
        computer.Clk = true;
        computer.Delay(1_000);

        Assert.IsTrue(computer.Cable.Data.IsLow, "the drive says it is there");
    }

    [TestMethod]
    [Timeout(60_000)]
    public void Listen_ShouldBeReceivedByTheDosAndKeepTheDriveOnTheBus()
    {
        var computer = new Computer(NewDrive());
        computer.Delay(2_000_000);

        computer.Atn = true;
        computer.Clk = true;
        computer.Delay(1_000);
        bool accepted = computer.SendByte(0x28);     // LISTEN 8

        Assert.IsTrue(accepted, "the DOS received the byte and answered: " + computer.Failure);
        Assert.IsTrue(computer.SendByte(0x6F), "secondary address: channel 15");
        computer.Atn = false;                        // the command is over: the listener stays on the bus
        computer.Delay(2_000);
        Assert.IsTrue(computer.SendByte(0x49), "a data byte for the command channel is acknowledged: " + computer.Failure);
    }

    [TestMethod]
    [Timeout(60_000)]
    public void Listen_ForAnotherDevice_ShouldNotBeAccepted()
    {
        var computer = new Computer(NewDrive());
        computer.Delay(2_000_000);

        computer.Atn = true;
        computer.Clk = true;
        computer.Delay(1_000);
        Assert.IsTrue(computer.SendByte(0x29), "during ATN every drive acknowledges");   // LISTEN 9
        computer.SendByte(0x6F);                     // a drive that was not addressed may or may not take part in this
        computer.Atn = false;
        computer.Delay(2_000);

        Assert.IsFalse(computer.SendByte(0x49), "device 8 was not addressed: nobody takes the byte");

    }

    [TestMethod]
    [Timeout(60_000)]
    public void DeviceNumber_ShouldBeSetByTheJumpers()
    {
        var computer = new Computer(NewDrive(device: 9));
        computer.Delay(2_000_000);

        computer.Atn = true;
        computer.Clk = true;
        computer.Delay(1_000);
        Assert.IsTrue(computer.SendByte(0x29));
        Assert.IsTrue(computer.SendByte(0x6F));
        computer.Atn = false;
        computer.Delay(2_000);

        Assert.IsTrue(computer.SendByte(0x49), "device 9 was addressed by LISTEN 9");
    }
}
