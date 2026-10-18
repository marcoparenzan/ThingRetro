namespace Thing65.WebStudio.CodeBehind;

// The page around a region editor: the source file may be open in Monaco too
public interface IRegionHost
{
    // False when the file has changes in its Monaco tab that are not saved (they would be lost)
    Task<bool> CanWriteAsync(string path);

    // The file was rewritten: its Monaco tab reloads it
    Task WrittenAsync(string path);
}
