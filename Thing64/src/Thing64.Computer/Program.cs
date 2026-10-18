using Thing64.C64;

namespace Thing64.Computer;

// The desktop app: one C64 in a window. Usually started by the thing64 command line ("thing64 run")
//   Thing64.Computer.exe [file.prg|file.crt]
static class Program
{
    [STAThread]
    static int Main(string[] args)
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
                "Thing64", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }

        Application.Run(new ComputerForm(roms, args.FirstOrDefault()) { Text = "Thing64" });
        return 0;
    }
}
