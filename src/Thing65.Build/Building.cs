using System.Text.Json;
using System.Text.Json.Serialization;
using Thing65.Asm;
using Thing65.Basic;
using Thing65.Crt;
using Thing65.Graphics;

namespace Thing65.Build;

// Turns sources into program files: .bas -> tokenized .prg, .asm -> assembled .prg (includes looked up next to the source).
// The assembler's processor is the C65's 4510 unless the caller (or a "!cpu" line in the source) says otherwise
public static class ProgramBuilder
{
    public static byte[] Build(string sourcePath, Cpu cpu = Cpu.Csg4510)
    {
        string text = File.ReadAllText(sourcePath);
        return Path.GetExtension(sourcePath).ToLowerInvariant() switch
        {
            ".bas" => BasicTokenizer.Tokenize(text),
            ".asm" or ".a" or ".s" => Assemble(sourcePath, text, cpu).Prg,
            _ => throw new NotSupportedException($"{Path.GetFileName(sourcePath)}: only .asm and .bas sources can be built"),
        };
    }

    public static Assembler.Result Assemble(string sourcePath, string? text = null, Cpu cpu = Cpu.Csg4510) =>
        Assembler.Assemble(text ?? File.ReadAllText(sourcePath), Path.GetFileName(sourcePath),
            Path.GetDirectoryName(Path.GetFullPath(sourcePath)), cpu);
}

// A build manifest (JSON) lists the files to produce from sources, with paths relative to the manifest's folder:
//   { "outputs": [
//       { "file": "hello.prg", "source": "src/hello.bas", "description": "..." },
//       { "file": "banks.crt", "description": "...", "cartridge": { "name": "BANKS", "type": 19,
//         "exromActive": true, "gameActive": false, "banks": [ { "bank": 0, "source": "src/banks0.asm" } ] } } ],
//     "assets": [ "gfx/title.gfx.json" ] }
// The assets are built first: graphics converted from images into the data and the include files the sources use.
// Without "assets", every .gfx.json in the manifest's folder (and below) is one.
// A cartridge bank is a source assembled at $8000 (ROML) or $E000 (ROMH, Ultimax), padded to 8 KB with $FF.
// samples/samples.json is the manifest of the samples; "thing65 build" runs one
public sealed class BuildManifest
{
    // The processor the sources are written for: "6510" (the default, the samples of the C64 board) or "4510"
    public string Cpu { get; set; } = "6510";

    public List<BuildOutput> Outputs { get; set; } = [];
    public List<string> Assets { get; set; } = [];

    [JsonIgnore]
    public string Folder { get; private set; } = "";

    static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static BuildManifest Load(string path)
    {
        var manifest = JsonSerializer.Deserialize<BuildManifest>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException($"{path}: empty manifest");
        manifest.Folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
        return manifest;
    }

    public string PathOf(string relative) => Path.GetFullPath(Path.Combine(Folder, relative));

    // Relative to the manifest's folder, with '/'
    public IReadOnlyList<string> AssetFiles() => Assets.Count > 0 ? Assets : FindAssets(Folder);

    public static IReadOnlyList<string> FindAssets(string folder) =>
        Directory.EnumerateFiles(folder, "*" + GraphicsAsset.Extension, SearchOption.AllDirectories)
            .Where(f => !Path.GetRelativePath(folder, f).Split(Path.DirectorySeparatorChar).Any(part => part.StartsWith('.')))
            .Select(f => Path.GetRelativePath(folder, f).Replace('\\', '/'))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public GraphicsBuilder.Result BuildAsset(string asset) => GraphicsBuilder.Build(PathOf(asset));

    public byte[] Build(BuildOutput output)
    {
        if (output.Cartridge is { } cartridge)
        {
            var chips = cartridge.Banks.Select(bank =>
            {
                var code = ProgramBuilder.Assemble(PathOf(bank.Source), cpu: ProcessorOfSources());
                return (bank.Bank, code.Origin, CrtWriter.Rom8K(code.Bytes));
            }).ToArray();
            return CrtWriter.Build(cartridge.Name, cartridge.Type, cartridge.ExromActive, cartridge.GameActive, chips);
        }
        if (output.Source is null) throw new InvalidDataException($"{output.File}: no source and no cartridge");
        return ProgramBuilder.Build(PathOf(output.Source), ProcessorOfSources());
    }

    Thing65.Asm.Cpu ProcessorOfSources() => Assembler.ParseCpu(Cpu);
}

public sealed record BuildOutput(string File, string? Description = null, string? Source = null, CartridgeSpec? Cartridge = null)
{
    public IEnumerable<string> Sources => Cartridge is { } c ? c.Banks.Select(b => b.Source) : Source is null ? [] : [Source];
}

public sealed record CartridgeSpec(string Name, int Type, bool ExromActive, bool GameActive, List<BankSpec> Banks);

public sealed record BankSpec(int Bank, string Source);
