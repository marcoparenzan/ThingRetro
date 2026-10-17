using System.Buffers.Binary;
using System.Text;

namespace Thing65.WebStudio.Assistant;

// A file attached to a message for RalfAI: saved in the project (.ralfai/attachments/), shown in the chat, and
// described at the end of the message: a text file with its content (when small), a picture by its path, for the
// Image_Look tool to see
public sealed record Attachment(string Name, string Path, bool IsImage, string? Preview, string Note);

public static class Attachments
{
    public const string Folder = ".ralfai/attachments";
    const int InlineLimit = 20_000;

    static readonly Dictionary<string, string> Pictures = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".gif"] = "image/gif", [".webp"] = "image/webp",
    };

    public static Attachment Save(Project project, string fileName, byte[] bytes)
    {
        string name = string.Concat(Path.GetFileName(fileName).Select(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '-'));
        if (name.Trim('.', '-').Length == 0) name = "attachment";
        string path = $"{Folder}/{DateTime.Now:yyyyMMdd-HHmmss}-{name}";
        string full = project.FullPath(path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, bytes);

        string extension = System.IO.Path.GetExtension(name);
        if (Pictures.TryGetValue(extension, out string? mediaType))
        {
            string size = extension.Equals(".png", StringComparison.OrdinalIgnoreCase) && bytes.Length >= 24
                ? $", {BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16))}x{BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20))}"
                : "";
            string? preview = bytes.Length <= 2_000_000 ? $"data:{mediaType};base64,{Convert.ToBase64String(bytes)}" : null;
            return new Attachment(name, path, true, preview, $"picture{size}: use Image_Look to see it");
        }

        // Text: no NUL bytes and valid UTF-8; the content goes into the message when it is small
        bool text = !bytes.Take(8192).Contains((byte)0);
        string note;
        if (text && bytes.Length <= InlineLimit)
            note = "\n```\n" + Encoding.UTF8.GetString(bytes).TrimEnd() + "\n```";
        else if (text)
            note = $"text, {bytes.Length} bytes: read it with Files_Read";
        else
            note = $"binary file, {bytes.Length} bytes";
        return new Attachment(name, path, false, null, note);
    }

    // What follows the user's text in the message
    public static string Describe(IReadOnlyList<Attachment> attachments) =>
        attachments.Count == 0 ? "" :
        "\n\nAttached files (in the project folder):\n" + string.Join("\n", attachments.Select(a => $"- `{a.Path}`: {a.Note}"));
}
