using System.Text.Json;
using System.Text.Json.Serialization;
using Thing64.Asm;
using Thing64.Basic;
using Thing64.Crt;
using Thing64.Graphics;

namespace Thing64.Build;

// Turns sources into C64 files: .bas -> tokenized .prg, .asm -> assembled .prg (includes looked up next to the source)
public static class ProgramBuilder
{
    public static byte[] Build(string sourcePath)
    {
        string text = File.ReadAllText(sourcePath);
        return Path.GetExtension(sourcePath).ToLowerInvariant() switch
        {
            ".bas" => BasicTokenizer.Tokenize(text),
            ".asm" or ".a" or ".s" => Assemble(sourcePath, text).Prg,
            _ => throw new NotSupportedException($"{Path.GetFileName(sourcePath)}: only .asm and .bas sources can be built"),
        };
    }

    public static Assembler.Result Assemble(string sourcePath, string? text = null) =>
        Assembler.Assemble(text ?? File.ReadAllText(sourcePath), Path.GetFileName(sourcePath),
            Path.GetDirectoryName(Path.GetFullPath(sourcePath)));
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
// samples/samples.json is the manifest of the samples; "thing64 build" runs one
public sealed class BuildManifest
{
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
                var code = ProgramBuilder.Assemble(PathOf(bank.Source));
                return (bank.Bank, code.Origin, CrtWriter.Rom8K(code.Bytes));
            }).ToArray();
            return CrtWriter.Build(cartridge.Name, cartridge.Type, cartridge.ExromActive, cartridge.GameActive, chips);
        }
        if (output.Source is null) throw new InvalidDataException($"{output.File}: no source and no cartridge");
        return ProgramBuilder.Build(PathOf(output.Source));
    }
}

public sealed record BuildOutput(string File, string? Description = null, string? Source = null, CartridgeSpec? Cartridge = null)
{
    public IEnumerable<string> Sources => Cartridge is { } c ? c.Banks.Select(b => b.Source) : Source is null ? [] : [Source];
}

public sealed record CartridgeSpec(string Name, int Type, bool ExromActive, bool GameActive, List<BankSpec> Banks);

public sealed record BankSpec(int Bank, string Source);
