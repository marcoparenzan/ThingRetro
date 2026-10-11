using Fake64.Cartridges;

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

            form.FileDropped += path => Open(form, board, path);

            // Fake64.exe file.prg|file.crt: una cartuccia si inserisce subito (riavvia la macchina),
            // un programma si carica dopo il boot
            if (args.Length > 0)
            {
                form.Shown += async (_, _) =>
                {
                    if (!IsCartridge(args[0])) await Task.Delay(BootTime);
                    Open(form, board, args[0]);
                };
            }

            Application.Run(form);
        }

        static bool IsCartridge(string path) =>
            string.Equals(Path.GetExtension(path), ".crt", StringComparison.OrdinalIgnoreCase);

        static void Open(Fake64Form form, Board board, string path)
        {
            byte[] content;
            try
            {
                content = File.ReadAllBytes(path);
            }
            catch (IOException e)
            {
                MessageBox.Show(form, e.Message, "Fake64", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (IsCartridge(path))
                InsertCartridge(form, board, path, content);
            else
                LoadProgram(form, board, path, content);
        }

        static void InsertCartridge(Fake64Form form, Board board, string path, byte[] crt)
        {
            Cartridge cartridge;
            try
            {
                cartridge = CrtFile.Load(crt);
            }
            catch (Exception e) when (e is InvalidDataException or NotSupportedException)
            {
                MessageBox.Show(form, e.Message, $"Fake64 - {Path.GetFileName(path)}", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            board.Enqueue(() => board.InsertCartridge(cartridge));
            var name = string.IsNullOrEmpty(cartridge.Name) ? Path.GetFileName(path) : cartridge.Name;
            form.Text = $"Fake64 - {name} ({cartridge.TypeName})";
        }

        static void LoadProgram(Fake64Form form, Board board, string path, byte[] prg)
        {
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
