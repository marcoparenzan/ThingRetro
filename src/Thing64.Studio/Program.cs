using Thing64.C64;
using Thing64.Computer;

namespace Thing64.Studio;

// The desktop app: the studio, or a single C64 window. Usually started by the thing64 command line
//   Thing64.Studio.exe [file.asm|file.prg|file.crt|folder]   the studio
//   Thing64.Studio.exe --computer [file.prg|file.crt]        one C64 window (the studio's Shift+F5, "thing64 run")
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

        if (args.Length > 0 && args[0] == "--computer")
            Application.Run(new ComputerForm(roms, args.ElementAtOrDefault(1)) { Text = "Thing64" });
        else
            Application.Run(new StudioForm(roms, args.FirstOrDefault())
            {
                LaunchComputer = program => System.Diagnostics.Process.Start(Environment.ProcessPath!, ["--computer", program]),
            });
        return 0;
    }
}
