using Thing65.C65;

namespace Thing65.Computer;

// The disk in the drive as the app sees it: the image and the file it came from. What the C65 writes to the disk is kept in
// memory; the slot writes it back to the file when the disk is taken out or replaced, when the window closes, and on request.
// A disk made by the app for a program (no file) can only be saved with "Save as"; a write-protected one is never written
public sealed class DiskSlot
{
    D81Image? image;

    public string? Path { get; private set; }
    public D81Image? Image => image;
    public bool Modified => image?.Modified == true;

    // Puts another disk in (or none): the one that was in is saved first. Returns whether something was written
    public bool Set(D81Image? next, string? path)
    {
        bool saved = Save();
        image = next;
        Path = path;
        return saved;
    }

    // Writes the disk to its file if it was changed and has one
    public bool Save()
    {
        if (image is not { Modified: true } || Path is null || image.WriteProtected) return false;
        File.WriteAllBytes(Path, image.Data);
        image.MarkSaved();
        return true;
    }

    public void SaveAs(string path)
    {
        if (image is null) throw new InvalidOperationException("There is no disk in the drive");
        File.WriteAllBytes(path, image.Data);
        image.MarkSaved();
        Path = path;
    }
}
