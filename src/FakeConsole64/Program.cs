using FakeConsole64;

namespace Fake64
{
    static class Program
    {
        static CancellationTokenSource runCancellationTaskSource = default;
        static Task runTask = default;

        static async Task Main()
        {
            var screen = new InMemoryScreen();
            var board = new Board(screen);

            var writer = Console.Out;
            //writer = new StreamWriter($"{Guid.NewGuid()}.txt");

            Run(board.DebugCPU, writer);
            var task = Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true);
                        switch (key.Key)
                        {
                            case ConsoleKey.D1: // CPU
                                Run(board.DebugCPU, writer);
                                break;
                            case ConsoleKey.D2: // CIA1
                                Run(board.DebugCIA1, writer);
                                break;
                            case ConsoleKey.D3: // CIA2
                                Run(board.DebugCIA2, writer);
                                break;
                            case ConsoleKey.P:
                                board.DebugPause();
                                break;
                            case ConsoleKey.S:
                                board.StepPause();
                                break;
                        }
                    }
                }
            });

            Task.WaitAll(task);
        }

        private static void Run(Action<CancellationTokenSource, TextWriter> debug, TextWriter writer)
        {
            if (runTask is not null)
            {
                runCancellationTaskSource.CancelAfter(1);
                runCancellationTaskSource = null;
                runTask = null;
            }
            runCancellationTaskSource = new CancellationTokenSource();
            runTask = Task.Factory.StartNew(() => { 
                debug(runCancellationTaskSource, writer);
            }, runCancellationTaskSource.Token);
        }
    }
}
