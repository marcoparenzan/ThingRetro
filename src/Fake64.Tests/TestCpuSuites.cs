using System.Text.Json;

namespace Fake64.Tests;

// External test suites for the 6502 core, downloaded with tools/Download-CpuTests.ps1.
// They are not in the repository (GPL-3.0 and MIT licenses): when they are missing the tests are "Inconclusive"
[TestClass]
public sealed class CpuSuiteTests
{
    static readonly string External = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "TestData", "External");

    // Flat 64 KB RAM, without I/O: the "bare" 6502
    sealed class FlatBus : IBus
    {
        public readonly byte[] Memory = new byte[0x10000];
        public List<(ushort Address, byte Value, bool Write)>? Log;

        public byte Read(ushort address)
        {
            Log?.Add((address, Memory[address], false));
            return Memory[address];
        }

        public void Write(ushort address, byte value)
        {
            Log?.Add((address, value, true));
            Memory[address] = value;
        }
    }

    #region Klaus Dormann, 6502 functional test

    // All the documented instructions, flags and decimal mode. The program starts at $0400 and ends
    // in a "JMP *": at $3469 if all the tests passed, elsewhere at the point of the error
    [TestMethod]
    [Timeout(120_000)]
    public void KlausDormann_FunctionalTest_ShouldReachSuccessTrap()
    {
        string image = Path.Combine(External, "KlausDormann", "6502_functional_test.bin");
        if (!File.Exists(image)) Assert.Inconclusive("Test missing: run tools/Download-CpuTests.ps1 -SkipSingleStep");

        var bus = new FlatBus();
        File.ReadAllBytes(image).CopyTo(bus.Memory, 0);
        bus.Memory[0xFFFC] = 0x00; // the file's reset vector points elsewhere: start from $0400
        bus.Memory[0xFFFD] = 0x04;
        var cpu = new MOS6502 { Bus = bus };
        cpu.Reset();

        ushort previous = 0;
        long instructions = 0;
        while (true)
        {
            do cpu.Tick(); while (!cpu.IsInstructionBoundary);
            instructions++;
            if (cpu.PC == previous) break; // JMP * : end of the test
            previous = cpu.PC;
            if (instructions > 100_000_000) Assert.Fail($"The test does not end (PC=${cpu.PC:X4})");
        }

        Assert.AreEqual(0x3469, (int)cpu.PC, $"Error trap at ${cpu.PC:X4}: see 6502_functional_test.lst");
    }

    #endregion

    #region Tom Harte, SingleStepTests 6502/v1

    // Excluded opcodes: the JAMs (the CPU halts, the bus behaviour is not an instruction) and the unstable
    // ones, which depend on the individual chip and on temperature
    static readonly HashSet<int> Excluded =
    [
        0x02, 0x12, 0x22, 0x32, 0x42, 0x52, 0x62, 0x72, 0x92, 0xB2, 0xD2, 0xF2, // JAM
        0x8B, 0xAB,                                                             // XAA, LXA
        0x93, 0x9B, 0x9C, 0x9E, 0x9F,                                           // SHA, TAS, SHY, SHX
    ];

    public static IEnumerable<object[]> Opcodes() => Enumerable.Range(0, 256).Select(opcode => new object[] { opcode });

    public static string OpcodeName(System.Reflection.MethodInfo method, object[] data) => $"SingleStep ${(int)data[0]:X2}";

    // Each case: initial state (registers and RAM), expected final state and bus accesses cycle by cycle
    [TestMethod]
    [DynamicData(nameof(Opcodes), DynamicDataDisplayName = nameof(OpcodeName))]
    public void SingleStep_ShouldMatchStateAndBusCycles(int opcode)
    {
        string file = Path.Combine(External, "SingleStepTests", "6502", $"{opcode:x2}.json");
        if (Excluded.Contains(opcode)) Assert.Inconclusive("Unstable or JAM opcode: excluded");
        if (!File.Exists(file)) Assert.Inconclusive("File missing: run tools/Download-CpuTests.ps1");

        using var document = JsonDocument.Parse(File.ReadAllBytes(file));
        var failures = new List<string>();
        int total = 0;

        foreach (var test in document.RootElement.EnumerateArray())
        {
            total++;
            string? failure = RunCase(test);
            if (failure is not null && failures.Count < 5) failures.Add(failure);
            else if (failure is not null) failures.Add("");
        }

        if (failures.Count > 0)
            Assert.Fail($"{failures.Count} cases out of {total} failed, for example:\n" + string.Join("\n", failures.Where(f => f.Length > 0)));
    }

    static string? RunCase(JsonElement test)
    {
        var bus = new FlatBus();
        var initial = test.GetProperty("initial");
        foreach (var cell in initial.GetProperty("ram").EnumerateArray())
            bus.Memory[cell[0].GetInt32()] = (byte)cell[1].GetInt32();

        var cpu = new MOS6502 { Bus = bus };
        cpu.Reset(); // then the state is set by hand
        cpu.PC = (ushort)initial.GetProperty("pc").GetInt32();
        cpu.SP = (byte)initial.GetProperty("s").GetInt32();
        cpu.A = (byte)initial.GetProperty("a").GetInt32();
        cpu.X = (byte)initial.GetProperty("x").GetInt32();
        cpu.Y = (byte)initial.GetProperty("y").GetInt32();
        cpu.Status = (byte)initial.GetProperty("p").GetInt32();
        foreach (var cell in initial.GetProperty("ram").EnumerateArray())
            bus.Memory[cell[0].GetInt32()] = (byte)cell[1].GetInt32();

        bus.Log = [];
        do cpu.Tick(); while (!cpu.IsInstructionBoundary);

        string name = test.GetProperty("name").GetString()!;
        var expectedCycles = test.GetProperty("cycles");
        if (expectedCycles.GetArrayLength() != bus.Log.Count)
            return $"{name}: {bus.Log.Count} cycles instead of {expectedCycles.GetArrayLength()}";
        int index = 0;
        foreach (var expected in expectedCycles.EnumerateArray())
        {
            var (address, value, write) = bus.Log[index];
            bool expectedWrite = expected[2].GetString() == "write";
            if (address != expected[0].GetInt32() || value != expected[1].GetInt32() || write != expectedWrite)
                return $"{name}: cycle {index} = {(write ? "W" : "R")} ${address:X4}={value:X2}, expected {(expectedWrite ? "W" : "R")} ${expected[0].GetInt32():X4}={expected[1].GetInt32():X2}";
            index++;
        }

        var final = test.GetProperty("final");
        var registers = new (string Name, int Actual)[]
        {
            ("pc", cpu.PC), ("s", cpu.SP), ("a", cpu.A), ("x", cpu.X), ("y", cpu.Y), ("p", cpu.Status),
        };
        foreach (var (register, actual) in registers)
        {
            int expected = final.GetProperty(register).GetInt32();
            if (actual != expected) return $"{name}: {register} = ${actual:X2}, expected ${expected:X2}";
        }
        foreach (var cell in final.GetProperty("ram").EnumerateArray())
        {
            int address = cell[0].GetInt32();
            if (bus.Memory[address] != cell[1].GetInt32())
                return $"{name}: RAM ${address:X4} = ${bus.Memory[address]:X2}, expected ${cell[1].GetInt32():X2}";
        }
        return null;
    }

    #endregion
}
