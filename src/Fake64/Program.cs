using Fake64.C64;

namespace Fake64
{
    static class Program
    {
        // Fake64.exe [file.prg|file.crt]
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
                MessageBox.Show($"Impossibile leggere le ROM del C64 da {RomFolder.DefaultPath}:\n{e.Message}",
                    "Fake64", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Application.Run(new Fake64Form(roms, args.FirstOrDefault()));
        }
    }
}
