using Fake64.C64;
using Fake64.Studio;

namespace Fake64;

static class Program
{
    // Fake64.exe [file.asm|file.prg|file.crt]: opens the studio, with the file in an editor or in a C64 tab
    // Fake64.exe --c64 [file.prg|file.crt]: just a C64 window, running the file (the studio's Shift+F5)
    [STAThread]
    static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        SystemRoms roms;
        try
        {
            roms = RomFolder.Load();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show($"Cannot read the C64 ROMs from {RomFolder.DefaultPath}:\n{e.Message}",
                "Fake64", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (args.Length > 0 && args[0] == "--c64")
            Application.Run(new Fake64Form(roms, args.ElementAtOrDefault(1)) { Text = "Fake64" });
        else
            Application.Run(new StudioForm(roms, args.FirstOrDefault()));
    }
}
