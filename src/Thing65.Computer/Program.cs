using Thing65.C65;

namespace Thing65.Computer;

// The desktop app: one C65 in a window. Usually started by the thing65 command line ("thing65 run")
//   Thing65.Computer.exe [file.prg|file.d81]
static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        SystemRom128 rom;
        try
        {
            rom = RomFolder.Load();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(e.Message, "Thing65", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 1;
        }

        Application.Run(new ComputerForm(rom, args.FirstOrDefault()) { Text = "Thing65" });
        return 0;
    }
}
