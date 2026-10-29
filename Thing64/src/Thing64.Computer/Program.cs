using Thing64.C64;

namespace Thing64.Computer;

// The desktop app: one C64 in a window. Usually started by the thing64 command line ("thing64 run")
//   Thing64.Computer.exe [--roms folder] [file.prg|file.crt]
// --roms takes the kernal, basic and chargen files from another folder (another operating system, such as ThingOS)
static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        string? romFolder = null;
        if (args.Length >= 2 && args[0] == "--roms")
        {
            romFolder = Path.GetFullPath(args[1]);
            args = args[2..];
        }

        SystemRoms roms;
        try
        {
            roms = RomFolder.Load(romFolder);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show($"Cannot read the C64 ROMs from {romFolder ?? RomFolder.DefaultPath}:\n{e.Message}",
                "Thing64", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }

        Application.Run(new ComputerForm(roms, args.FirstOrDefault()) { Text = "Thing64" });
        return 0;
    }
}
