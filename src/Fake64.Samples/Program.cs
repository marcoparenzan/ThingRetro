using Fake64.Samples;

// Uso: dotnet run --project src/Fake64.Samples [cartella]   (predefinita: samples nella radice del repository)
string output = args.Length > 0 ? args[0] : Path.Combine(FindRepositoryRoot(), "samples");
Directory.CreateDirectory(output);

foreach (var sample in SampleSet.All)
{
    File.WriteAllBytes(Path.Combine(output, sample.FileName), sample.Content);
    if (sample.BasicSource is not null)
        File.WriteAllText(Path.Combine(output, Path.ChangeExtension(sample.FileName, ".bas")), sample.BasicSource);
    Console.WriteLine($"{sample.FileName,-16} {sample.Content.Length,6} byte  {sample.Description}");
}

static string FindRepositoryRoot()
{
    for (var directory = new DirectoryInfo(Environment.CurrentDirectory); directory is not null; directory = directory.Parent)
        if (Directory.Exists(Path.Combine(directory.FullName, ".git"))) return directory.FullName;
    return Environment.CurrentDirectory;
}
