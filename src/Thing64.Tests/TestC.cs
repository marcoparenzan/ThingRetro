using Thing64.Asm;
using Thing64.C;
using Thing64.Mos;

namespace Thing64.Tests;

// Thing64.C: C translated to assembly, assembled, run on the bare 6502, the variables read back from memory
[TestClass]
public sealed class CTests
{
    sealed class FlatBus : IBus
    {
        public readonly byte[] Memory = new byte[0x10000];
        public byte Read(ushort address) => Memory[address];
        public void Write(ushort address, byte value) => Memory[address] = value;
    }

    // The program: JSR main, then a loop that marks the end. Returns the memory and the symbols
    sealed record Run(byte[] Memory, IReadOnlyDictionary<string, int> Symbols, string Assembly)
    {
        public int Byte(string name) => Memory[Symbols[name]];
        public int SignedByte(string name) => (sbyte)Memory[Symbols[name]];
        public int Word(string name) => Memory[Symbols[name]] | Memory[Symbols[name] + 1] << 8;
        public int SignedWord(string name) => (short)Word(name);
    }

    static Run Execute(string c)
    {
        string assembly = Generator.Translate(c, "test");
        var program = Assembler.Assemble("* = $1000\nstart:  JSR main\ndone:   JMP done\n" + assembly, "test.asm");
        var bus = new FlatBus();
        program.Bytes.CopyTo(bus.Memory, program.Origin);
        var cpu = new MOS6502 { Bus = bus };
        cpu.PC = (ushort)program.Symbols["start"];
        cpu.SP = 0xFF;
        cpu.Status = 0x24;
        int done = program.Symbols["done"];
        for (long cycle = 0; cpu.PC != done; cycle++)
        {
            if (cycle > 5_000_000) Assert.Fail("The program did not end\n" + assembly);
            cpu.Tick();
        }
        return new Run(bus.Memory, program.Symbols, assembly);
    }

    [TestMethod]
    public void Arithmetic_ShouldWorkOn8And16Bits()
    {
        var run = Execute("""
            char a = 5;
            char b;
            int w;
            unsigned int u = 60000;
            void main(void) {
                b = a + 3;
                w = 1000 + a;
                u = u + 6000;          // wraps around at 65536
                a = (a ^ 0x0F) | 0x80;
            }
            """);
        Assert.AreEqual(8, run.Byte("b"));
        Assert.AreEqual(1005, run.Word("w"));
        Assert.AreEqual((60000 + 6000) & 0xFFFF, run.Word("u"));
        Assert.AreEqual((5 ^ 0x0F) | 0x80, run.Byte("a"));
    }

    [TestMethod]
    public void Loops_ShouldSumNumbers()
    {
        var run = Execute("""
            char small;
            int big;
            int count;
            void main(void) {
                char i;
                int j;
                for (i = 1; i <= 10; i++) small += i;
                j = 1;
                while (j <= 100) { big = big + j; j++; }
                do { count++; } while (count < 7);
            }
            """);
        Assert.AreEqual(55, run.Byte("small"));
        Assert.AreEqual(5050, run.Word("big"));
        Assert.AreEqual(7, run.Word("count"));
    }

    [TestMethod]
    public void Comparisons_ShouldRespectSigns()
    {
        var run = Execute("""
            signed char s = -5;
            char u = 250;
            int i = -300;
            unsigned int w = 40000;
            char r1; char r2; char r3; char r4; char r5; char r6;
            void main(void) {
                if (s < 3) r1 = 1; else r1 = 2;          // signed: -5 < 3
                if (u > 3) r2 = 1; else r2 = 2;          // unsigned: 250 > 3
                if (i < 200) r3 = 1; else r3 = 2;        // signed 16 bits
                if (w > 30000) r4 = 1; else r4 = 2;      // unsigned 16 bits
                r5 = (i == -300) && !(u != 250);
                r6 = s >= 0 || i > 0 ? 7 : 9;
            }
            """);
        Assert.AreEqual(1, run.Byte("r1"));
        Assert.AreEqual(1, run.Byte("r2"));
        Assert.AreEqual(1, run.Byte("r3"));
        Assert.AreEqual(1, run.Byte("r4"));
        Assert.AreEqual(1, run.Byte("r5"));
        Assert.AreEqual(9, run.Byte("r6"));
    }

    [TestMethod]
    public void Functions_ShouldTakeParametersAndReturnValues()
    {
        var run = Execute("""
            int add(int a, int b) { return a + b; }
            char twice(char x) { return x + x; }
            int w; int nested; char c;
            void main(void) {
                w = add(300, 400);
                nested = add(1, add(2, 3));
                c = twice(twice(5));
            }
            """);
        Assert.AreEqual(700, run.Word("w"));
        Assert.AreEqual(6, run.Word("nested"));
        Assert.AreEqual(20, run.Byte("c"));
        StringAssert.Contains(run.Assembly, "add__a");
    }

    [TestMethod]
    public void ArraysAndPointers_ShouldReadAndWriteMemory()
    {
        var run = Execute("""
            char buf[10];
            int table[4] = { 1000, 2000, 3000, 4000 };
            char text[] = "HELLO";
            char nine; char viaPointer; int third; char length; int sum;
            void main(void) {
                char i;
                char *p;
                int *q;
                for (i = 0; i < 10; i++) buf[i] = i * 2;
                nine = buf[9];
                p = buf;
                viaPointer = *(p + 4) + p[1];
                third = table[2];
                q = table;
                q++;
                sum = *q + q[2];
                p = text;
                while (*p) { length++; p++; }
            }
            """);
        Assert.AreEqual(18, run.Byte("nine"));
        Assert.AreEqual(8 + 2, run.Byte("viaPointer"));
        Assert.AreEqual(3000, run.Word("third"));
        Assert.AreEqual(2000 + 4000, run.Word("sum"));
        Assert.AreEqual(5, run.Byte("length"));
    }

    [TestMethod]
    public void MultiplicationAndDivision_ShouldUseTheLibrary()
    {
        var run = Execute("""
            int product; int quotient; int remainder; int negative; int negativeRemainder; char shifted; int big;
            void main(void) {
                int a = 123;
                int b = 45;
                int n = -7;
                product = a * b;
                quotient = 1000 / (b - 38);
                remainder = 1000 % (b - 38);
                negative = n / 2;
                negativeRemainder = n % 2;
                shifted = a * 4;
                big = 1 << 10;
            }
            """);
        Assert.AreEqual(123 * 45, run.Word("product"));
        Assert.AreEqual(1000 / 7, run.Word("quotient"));
        Assert.AreEqual(1000 % 7, run.Word("remainder"));
        Assert.AreEqual(-3, run.SignedWord("negative"));
        Assert.AreEqual(-1, run.SignedWord("negativeRemainder"));
        Assert.AreEqual((123 * 4) & 0xFF, run.Byte("shifted"));
        Assert.AreEqual(1024, run.Word("big"));
    }

    [TestMethod]
    public void Shifts_ShouldKeepTheSign()
    {
        var run = Execute("""
            int right; signed char small; char variable; unsigned int logical;
            void main(void) {
                int a = -64;
                signed char s = -16;
                char n = 3;
                right = a >> 2;
                small = s >> 1;
                variable = 1 << n;
                logical = 0x8000 >> 3;
            }
            """);
        Assert.AreEqual(-16, run.SignedWord("right"));
        Assert.AreEqual(-8, run.SignedByte("small"));
        Assert.AreEqual(8, run.Byte("variable"));
        Assert.AreEqual(0x1000, run.Word("logical"));
    }

    [TestMethod]
    public void IncrementsAndCompoundAssignments_ShouldWork()
    {
        var run = Execute("""
            char c = 10; int w = 255; int before; int after; char mask = 0xF0;
            void main(void) {
                before = w++;            // 255, then 256
                after = ++w;             // 257
                c -= 3;
                c <<= 1;
                mask &= 0x3C;
                --c;
            }
            """);
        Assert.AreEqual(255, run.Word("before"));
        Assert.AreEqual(257, run.Word("after"));
        Assert.AreEqual(257, run.Word("w"));
        Assert.AreEqual((10 - 3) * 2 - 1, run.Byte("c"));
        Assert.AreEqual(0xF0 & 0x3C, run.Byte("mask"));
    }

    [TestMethod]
    public void HardwareRegisters_ShouldBeWrittenDirectly()
    {
        var run = Execute("""
            #define VIC 0xD000
            #define BORDER (*(char*)(VIC + 0x20))
            #define BACKGROUND (*(char*)(VIC + 0x21))
            void main(void) {
                BORDER = 2;
                BACKGROUND = BORDER + 4;
            }
            """);
        Assert.AreEqual(2, run.Memory[0xD020]);
        Assert.AreEqual(6, run.Memory[0xD021]);
        StringAssert.Contains(run.Assembly, "STA $D020");
    }

    [TestMethod]
    public void Recursion_ShouldBeRefused()
    {
        var error = Assert.ThrowsExactly<CException>(() => Generator.Translate("""
            int down(int n) { return n == 0 ? 0 : down(n - 1); }
            void main(void) { down(3); }
            """, "test"));
        StringAssert.Contains(error.Message, "recursion");
        Assert.AreEqual(1, error.Line);
    }

    [TestMethod]
    public void Errors_ShouldTellTheLine()
    {
        var error = Assert.ThrowsExactly<CException>(() => Generator.Translate("""
            void main(void) {
                char a;
                a = b + 1;
            }
            """, "test"));
        Assert.AreEqual(3, error.Line);
        StringAssert.Contains(error.Message, "b is not declared");
    }
}
