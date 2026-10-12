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

            Application.Run(new Fake64Form(args.FirstOrDefault()));
        }
    }
}
