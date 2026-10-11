namespace Fake64
{
    static class Program
    {
        // Tempo lasciato al KERNAL per arrivare a READY. prima di caricare un .prg dalla riga di comando
        static readonly TimeSpan BootTime = TimeSpan.FromSeconds(3);

        [STAThread]
        static void Main(string[] args)
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var form = new Fake64Form();
            var board = new Board(form);

            form.PrgDropped += path => Load(form, board, path);

            // Fake64.exe programma.prg: carica ed esegue il programma dopo il boot
            if (args.Length > 0)
            {
                form.Shown += async (_, _) =>
                {
                    await Task.Delay(BootTime);
                    Load(form, board, args[0]);
                };
            }

            Application.Run(form);
        }

        static void Load(Fake64Form form, Board board, string path)
        {
            byte[] prg;
            try
            {
                prg = File.ReadAllBytes(path);
            }
            catch (IOException e)
            {
                MessageBox.Show(form, e.Message, "Fake64", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            board.Enqueue(() =>
            {
                string title;
                try
                {
                    var (start, end) = board.LoadPrg(prg);
                    if (board.IsBasicProgram(start))
                    {
                        board.TypeText("RUN\r");
                        title = $"{Path.GetFileName(path)} (BASIC)";
                    }
                    else
                    {
                        title = $"{Path.GetFileName(path)} ${start:X4}-${end - 1:X4}: SYS {start}";
                    }
                }
                catch (ArgumentException e)
                {
                    title = e.Message;
                }
                form.BeginInvoke(() => form.Text = $"Fake64 - {title}");
            });
        }
    }
}
