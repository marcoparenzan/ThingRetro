namespace Thing65.WebStudio.CodeBehind;

// What the page asks of every region editor (tilemap, sprites, ...)
public interface IRegionEditor
{
    // From the file and the program again
    void Load();

    // The region written back into its file; false when it could not be (the file has unsaved changes elsewhere)
    Task<bool> Save();
}
