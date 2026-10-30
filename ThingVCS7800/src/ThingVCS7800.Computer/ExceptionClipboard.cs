using System.Runtime.InteropServices;

namespace ThingVCS7800.Computer;

// Temporary diagnostic aid. Clipboard failures must never replace the original error.
internal static class ExceptionClipboard
{
    public static bool TryCopy(Exception exception)
    {
        try
        {
            var data = new DataObject();
            string details = exception.ToString();
            if (exception.Data["ROM absolute path"] is string romPath)
                details = $"ROM file: {Path.GetFileName(romPath)}\nROM: {romPath}\n\n{details}";
            data.SetText(details);
            Clipboard.SetDataObject(data, true, 3, 100);
            return true;
        }
        catch (Exception ex) when (ex is ExternalException or ThreadStateException)
        {
            System.Diagnostics.Debug.WriteLine($"Cannot copy exception to clipboard: {ex}");
            return false;
        }
    }
}
