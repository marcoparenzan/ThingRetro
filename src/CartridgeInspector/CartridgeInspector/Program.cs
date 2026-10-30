using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

if (args.Length is < 1 or > 2) { Console.Error.WriteLine("Usage: CartridgeInspector <file.a78> [frame.ppm] | --dli-diagnostic [output-directory]"); return 2; }
try
{
    if (args[0] == "--tia-audio-diagnostic")
    {
        string directory = args.Length == 2 ? args[1] : "ops/references/tia-audio-diagnostic";
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "tia-tone-noise.bin"), ThingVCS.DevelopmentRoms.TiaSound());
        var results = new List<ThingVCS7800.A7800.TiaAudioResult>();
        foreach (var system in new[] { ThingRetroEmu.Video.VideoSystem.Ntsc, ThingRetroEmu.Video.VideoSystem.Pal })
        {
            using var output = File.Create(Path.Combine(directory, $"tia-{system}.wav"));
            var result = new ThingVCS7800.A7800.TiaAudioDiagnostic().Run(output, system);
            results.Add(result);
            Console.WriteLine($"{system}: samples={result.Samples}, audio-clocks={result.AudioClocks}, CPU-registers={result.RegistersProgrammed}, passed={result.Passed}");
        }
        File.WriteAllText(Path.Combine(directory, "result.json"), System.Text.Json.JsonSerializer.Serialize(results,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } }));
        return results.All(result => result.Passed) ? 0 : 1;
    }
    if (args[0] == "--dli-matrix")
    {
        var systems = new[] { ThingRetroEmu.Video.VideoSystem.Ntsc, ThingRetroEmu.Video.VideoSystem.Pal };
        var results = systems.SelectMany(system => new[] { false, true }.Select(startup =>
            new ThingVCS7800.A7800.DliTimingDiagnostic().Run(startupDli: startup, system: system))).ToArray();
        foreach (var result in results)
            Console.WriteLine($"{result.System}, startup={result.StartupDli}: edges={result.ObservedEdges}, serviced={result.ServicedInterrupts}, unserviced={result.UnservicedInterrupts}, min-spacing={result.MinimumEdgeSpacing}, handler-observation-latency={result.MinimumHandlerLatency}..{result.MaximumHandlerLatency}, passed={result.Passed}");
        if (args.Length == 2)
        {
            Directory.CreateDirectory(args[1]);
            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
            File.WriteAllText(Path.Combine(args[1], "dli-matrix.json"), System.Text.Json.JsonSerializer.Serialize(results, options));
            foreach (var system in systems)
                foreach (bool startup in new[] { false, true })
                    File.WriteAllBytes(Path.Combine(args[1], $"dli-{system}-startup-{startup}.bin"),
                        ThingVCS.DevelopmentRoms.DisplayListInterrupts(startup, ThingVCS.MariaTiming.For(system).ActiveLines));
        }
        return results.All(result => result.Passed) ? 0 : 1;
    }
    if (args[0] == "--dli-diagnostic")
    {
        var result = new ThingVCS7800.A7800.DliTimingDiagnostic().Run();
        string json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } });
        Console.WriteLine(json);
        if (args.Length == 2)
        {
            Directory.CreateDirectory(args[1]);
            File.WriteAllBytes(Path.Combine(args[1], "post-render-dli-ntsc.bin"), ThingVCS.DevelopmentRoms.DisplayListInterrupts(startupDli: false));
            File.WriteAllText(Path.Combine(args[1], "post-render-dli-result.json"), json);
        }
        return result.Passed ? 0 : 1;
    }
    string path = Path.GetFullPath(args[0]);
    byte[] data = File.ReadAllBytes(path);
    Console.WriteLine($"ROM: {path}\nFile bytes: {data.Length}\nSHA256: {Convert.ToHexString(SHA256.HashData(data))}");
    if (data.Length < 128 || !data.AsSpan(1, 9).SequenceEqual("ATARI7800"u8))
        throw new InvalidDataException("Missing or truncated A78 header.");
    Console.WriteLine($"Header version: {data[0]}\nTitle: {Encoding.ASCII.GetString(data, 17, 32).TrimEnd('\0', ' ')}\nDeclared ROM: {BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(49, 4))}\nActual ROM: {data.Length - 128}\nMapper flags: ${BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(53, 2)):X4}\nControllers: ${data[55]:X2}/${data[56]:X2}\nTV: ${data[57]:X2}\nSave peripheral: ${data[58]:X2}\nHeader hex: {Convert.ToHexString(data.AsSpan(0, 128))}");
    Console.WriteLine($"Payload SHA1: {Convert.ToHexString(SHA1.HashData(data.AsSpan(128)))}");
    if (ThingVCS.CartridgeImage.HasKnownDevelopmentCard(data))
        Console.WriteLine("Known Atari Development Card: MAME catalog supported=no; assigned POKEY slot explicitly marked wrong. Development-board hardware required; no override inferred.");
    if (args.Length == 2)
    {
        var computer = new ThingVCS7800.A7800.DevelopmentComputer();
        bool knownRam = ThingVCS.CartridgeImage.HasKnownMissingRam(data);
        Console.WriteLine($"Known payload missing-RAM correction applied in diagnostic run: {knownRam}");
        computer.InsertImage(data, true, allowKnownRamCorrection: knownRam);
        var maria = (ThingVCS.Maria)computer.Board.MariaSocket.Chip!;
        using var trace = new StreamWriter(args[1] + ".dma.txt");
        maria.DmaTrace = line =>
        {
            if (line.Frame != 179 || line.Terminated) return;
            trace.WriteLine($"Line {line.Scanline}: DLL=${line.Dll:X4} DL=${line.DisplayList:X4} offset={line.ZoneOffset} holey={line.Holey} budget={line.Budget} exhausted={line.Exhausted}");
            foreach (var obj in line.Objects) trace.WriteLine($"  Header=${obj.Header:X4} graphics=${obj.Graphics:X4} width={obj.Width} x={obj.Position} palette={obj.Palette} indirect={obj.Indirect} write={obj.WriteMode}");
        };
        for (int frame = 0; frame < 180; frame++)
        {
            long target = maria.Frames + 1;
            while (maria.Frames < target) computer.Board.Tick();
            if (frame % 30 == 29) Console.WriteLine($"Frame {maria.Frames}: DLL=${maria.DisplayListPointer:X4}, CTRL=${maria.Control:X2}, reads={maria.GraphicsReads}, truncated={maria.TruncatedLines}, DLI={maria.DisplayListInterrupts}, DLI overhead={maria.DliOverheadMasterClocks}, clamped={maria.DliOverheadClampedLines}");
        }
        using var stream = File.Create(args[1]);
        byte[] header = Encoding.ASCII.GetBytes($"P6\n{maria.Mode.Width} {maria.Mode.Height}\n255\n");
        stream.Write(header);
        foreach (byte pixel in maria.Framebuffer.Span)
        {
            int rgb = ThingVCS.Maria.ApproximateArgb(pixel);
            stream.WriteByte((byte)(rgb >> 16)); stream.WriteByte((byte)(rgb >> 8)); stream.WriteByte((byte)rgb);
        }
        Console.WriteLine($"Framebuffer: {Path.GetFullPath(args[1])}");
    }
    return 0;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine(ex.Message); return 1;
}
