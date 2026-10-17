using Thing65.C65;

namespace Thing65.Computer;

// The desktop app: one C64 in a window. Usually started by the thing65 command line ("thing65 run")
//   Thing65.Computer.exe [file.prg|file.crt]
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
                "Thing65", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }

        Application.Run(new ComputerForm(roms, args.FirstOrDefault()) { Text = "Thing65" });
        return 0;
    }
}
