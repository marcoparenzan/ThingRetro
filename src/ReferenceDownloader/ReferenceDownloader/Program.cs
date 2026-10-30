using System.Security.Cryptography;
using System.Text.Json;

string output = Path.GetFullPath(args.FirstOrDefault() ?? "ops/references");
Directory.CreateDirectory(output);
(string Name, string Url)[] sources = [
    ("mame-a7800-driver.cpp", "https://raw.githubusercontent.com/mamedev/mame/master/src/mame/atari/a7800.cpp"),
    ("mame-a7800-software.xml", "https://raw.githubusercontent.com/mamedev/mame/master/hash/a7800.xml"),
    ("mame-a7800-rom.cpp", "https://raw.githubusercontent.com/mamedev/mame/master/src/devices/bus/a7800/rom.cpp"),
    ("mame-a7800-slot.cpp", "https://raw.githubusercontent.com/mamedev/mame/master/src/devices/bus/a7800/a78_slot.cpp"),
    ("a7800-fork-slot.cpp", "https://raw.githubusercontent.com/7800-devtools/a7800/master/src/devices/bus/a7800/a78_slot.cpp"),
    ("mame-maria.cpp", "https://raw.githubusercontent.com/mamedev/mame/master/src/mame/atari/maria.cpp"),
    ("mame-license.txt", "https://raw.githubusercontent.com/mamedev/mame/master/COPYING"),
    ("prosystem-maria.c", "https://raw.githubusercontent.com/libretro/prosystem-libretro/master/core/Maria.c"),
    ("prosystem-sally.c", "https://raw.githubusercontent.com/libretro/prosystem-libretro/master/core/Sally.c"),
    ("prosystem-license.txt", "https://raw.githubusercontent.com/libretro/prosystem-libretro/master/License.txt"),
    ("prosystem-memory.c", "https://raw.githubusercontent.com/libretro/prosystem-libretro/master/core/Memory.c"),
    ("prosystem-tia.c", "https://raw.githubusercontent.com/libretro/prosystem-libretro/master/core/Tia.c"),
    ("mame-pokey.cpp", "https://raw.githubusercontent.com/mamedev/mame/master/src/devices/sound/pokey.cpp"),
    ("mame-pokey.h", "https://raw.githubusercontent.com/mamedev/mame/master/src/devices/sound/pokey.h"),
    ("prosystem-pokey.c", "https://raw.githubusercontent.com/libretro/prosystem-libretro/master/core/Pokey.c"),
    ("stella-audio-channel.cxx", "https://raw.githubusercontent.com/stella-emu/stella/master/src/emucore/tia/AudioChannel.cxx"),
    ("stella-license.txt", "https://raw.githubusercontent.com/stella-emu/stella/master/License.txt")
];
using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
var entries = new List<object>();
foreach (var (name, url) in sources)
{
    byte[] bytes = await client.GetByteArrayAsync(url);
    await File.WriteAllBytesAsync(Path.Combine(output, name), bytes);
    entries.Add(new { File = name, Url = url, DownloadedUtc = DateTime.UtcNow, SHA256 = Convert.ToHexString(SHA256.HashData(bytes)) });
    Console.WriteLine($"Downloaded {name}: {bytes.Length} bytes");
}
await File.WriteAllTextAsync(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Complete: {output}");
