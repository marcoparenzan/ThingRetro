using Microsoft.Extensions.Logging;
namespace ThingVCS7800.Computer;
static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
        {
            bool copied = ExceptionClipboard.TryCopy(e.Exception);
            MessageBox.Show(e.Exception.Message + (copied ? "\n\nDettagli completi copiati negli appunti." : "\n\nImpossibile copiare i dettagli negli appunti."),
                "ThingVCS7800 — unexpected error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            // State after an unexpected UI error is unknown: close rather than continue emulation.
            Application.Exit();
        };
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using var logging = LoggerFactory.Create(builder => builder.AddDebug());
        Application.Run(new ComputerForm(logging.CreateLogger<ComputerForm>(), args.FirstOrDefault()));
    }
}
