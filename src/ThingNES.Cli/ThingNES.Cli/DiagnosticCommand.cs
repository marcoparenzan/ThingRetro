using ThingNES;

internal static class DiagnosticCommand
{
    public static async Task<int> RunAsync(string[] args, bool busConflicts = true)
    {
        if (args.Length is < 2 or > 3 || (args.Length == 3 && (!long.TryParse(args[2], out long n) || n <= 0)))
        { Console.Error.WriteLine("Usage: --diagnostic <rom.nes|https-url> [max-cpu-cycles]"); return 1; }
        long budget = args.Length == 3 ? long.Parse(args[2]) : 30_000_000;
        using var cts = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, e) => { e.Cancel = true; cts.Cancel(); };
        Console.CancelKeyPress += handler;
        try
        {
            byte[] image;
            if (Uri.TryCreate(args[1], UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
            {
                // Explicit URL only; never downloads ROMs during ordinary runs or unit tests.
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30), MaxResponseContentBufferSize = 1024 * 1024 };
                image = await client.GetByteArrayAsync(uri, cts.Token);
                Console.WriteLine($"Source: {uri}\nSHA256: {Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(image))}");
            }
            else image = await File.ReadAllBytesAsync(args[1], cts.Token);
            var result = DiagnosticRunner.Run(new NesMachine(Cartridge.Load(image, busConflicts)), budget, cts.Token);
            Console.WriteLine($"{result.Outcome}: code={result.Code?.ToString() ?? "none"}, cycles={result.Cycles}, signature={result.SignatureSeen}");
            if (result.Message.Length > 0) Console.WriteLine(result.Message);
            if (result.Outcome == DiagnosticOutcome.ResetRequested) Console.WriteLine("ROM requests a reset after >=100 ms; automatic board reset is not implemented.");
            return result.ExitCode;
        }
        catch (OperationCanceledException) { Console.Error.WriteLine("Cancelled or download timed out."); return 5; }
        catch (Exception ex) when (ex is IOException or HttpRequestException or NotSupportedException or UnauthorizedAccessException or ArgumentException)
        { Console.Error.WriteLine(ex.Message); return 1; }
        finally { Console.CancelKeyPress -= handler; }
    }
}
