using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;
using ThingMOS.Asm;
namespace ThingVCS7800.Web;

public sealed class StudioCompiler
{
    public const string Template = "* = $c000\nreset:\n    sei\n    cld\n    ldx #$ff\n    txs\n    lda #$3a\n    sta $20\n    lda #4\n    sta $15\n    lda #7\n    sta $17\n    lda #15\n    sta $19\nloop:\n    jmp loop\n";
    public byte[] Build(string source,string title)
    {
        if(string.IsNullOrWhiteSpace(source)||source.Length>32768) throw new ArgumentException("Source must contain 1–32768 characters.");
        // Single-file sandbox: reject file-reading directive names even inside macros/comments.
        if(Regex.IsMatch(source,@"\b(include|source|src|binary|bin|bi|for|while|repeat|do)\b",RegexOptions.IgnoreCase))
            throw new ArgumentException("File includes/binary imports and assembly loops are disabled in the web single-file editor.");
        var assembled=Assembler.Assemble(source);
        if(assembled.Origin!=0xc000 || assembled.Bytes.Length>16384) throw new ArgumentException("This Studio increment builds linear 16 KiB ROMs starting at $C000 only.");
        var rom=Enumerable.Repeat((byte)0xff,16384).ToArray(); assembled.Bytes.CopyTo(rom,0);
        int entry=assembled.Symbols.TryGetValue("reset",out int address)?address:0xc000;
        if(entry<0xc000||entry>0xffff) throw new ArgumentException("Reset entry outside cartridge ROM.");
        rom[0x3ffc]=(byte)entry;rom[0x3ffd]=(byte)(entry>>8);
        byte[] image=new byte[128+16384];image[0]=3;"ATARI7800"u8.CopyTo(image.AsSpan(1));
        Encoding.ASCII.GetBytes((title??"Untitled").PadRight(32)[..32]).CopyTo(image,17);
        BinaryPrimitives.WriteUInt32BigEndian(image.AsSpan(49),16384);image[55]=image[56]=1;
        "ACTUAL CART DATA STARTS HERE"u8.CopyTo(image.AsSpan(100));rom.CopyTo(image,128);
        return image;
    }
}
