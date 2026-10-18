using ThingCommodore.Build;
using Thing65.Cli;
using Spectre.Console.Cli;

namespace Thing65.Tests;

// The thing65 command line: the commands that do not open windows
[TestClass]
[DoNotParallelize]   // Spectre writes to the shared console
public sealed class CommandLineTests
{
    string folder = "";

    [TestInitialize]
    public void CreateFolder()
    {
        folder = Path.Combine(Path.GetTempPath(), "thing65-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
    }

    [TestCleanup]
    public void DeleteFolder() => Directory.Delete(folder, recursive: true);

    static int RunThing65(params string[] args)
    {
        var app = new CommandApp();
        app.Configure(config =>
        {
            config.AddCommand<AsmCommand>("asm");
            config.AddCommand<BasicCommand>("basic");
            config.AddCommand<BuildCommand>("build");
        });
        return app.Run(args);
    }

    string Write(string name, string text)
    {
        string path = Path.Combine(folder, name);
        File.WriteAllText(path, text);
        return path;
    }

    [TestMethod]
    public void Asm_ShouldWriteThePrgAndViceLabels()
    {
        string source = Write("main.asm", "* = $c000\nstart   lda #1\n        rts\n");

        int exit = RunThing65("asm", source, "--labels", Path.Combine(folder, "main.labels"));

        Assert.AreEqual(0, exit);
        CollectionAssert.AreEqual(new byte[] { 0x00, 0xC0, 0xA9, 0x01, 0x60 }, File.ReadAllBytes(Path.Combine(folder, "main.prg")));
        CollectionAssert.Contains(File.ReadAllLines(Path.Combine(folder, "main.labels")), "al C:C000 .start");
    }

    [TestMethod]
    public void Asm_WithErrors_ShouldFailWithoutOutput()
    {
        string source = Write("broken.asm", "* = $c000\n  lda #$100\n");

        Assert.AreEqual(1, RunThing65("asm", source));
        Assert.IsFalse(File.Exists(Path.Combine(folder, "broken.prg")));
    }

    [TestMethod]
    public void Basic_ShouldTokenize()
    {
        string source = Write("hello.bas", "10 PRINT \"HI\"\n");

        Assert.AreEqual(0, RunThing65("basic", source, "-o", Path.Combine(folder, "out.prg")));
        CollectionAssert.AreEqual(global::ThingCommodore.Basic.BasicTokenizer.Tokenize("10 PRINT \"HI\""), File.ReadAllBytes(Path.Combine(folder, "out.prg")));
    }

    [TestMethod]
    public void Build_ShouldProduceEveryOutputOfTheManifest()
    {
        Directory.CreateDirectory(Path.Combine(folder, "src"));
        Write("src/code.asm", "* = $8000\n!word start, start\n!byte $c3, $c2, $cd, '8', '0'\nstart jmp start\n");
        Write("src/prog.bas", "10 END\n");
        string manifest = Write("thing65.json", """
            {
              "outputs": [
                { "file": "prog.prg", "source": "src/prog.bas" },
                { "file": "cart.crt", "cartridge": { "name": "TEST", "type": 0, "exromActive": true, "gameActive": false,
                                                     "banks": [ { "bank": 0, "source": "src/code.asm" } ] } }
              ]
            }
            """);

        Assert.AreEqual(0, RunThing65("build", manifest));

        Assert.IsTrue(File.Exists(Path.Combine(folder, "prog.prg")));
        var cartridge = global::ThingCommodore.Cartridges.CrtFile.Load(File.ReadAllBytes(Path.Combine(folder, "cart.crt")));
        Assert.AreEqual("Normal 8K", cartridge.TypeName);
        Assert.AreEqual("TEST", cartridge.Name);
    }
}
