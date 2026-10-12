using Fake64.Samples;

// Uso: dotnet run --project src/Fake64.Samples [cartella]   (predefinita: samples nella radice del repository)
// Scrive i file degli esempi; i sorgenti stanno in samples/src
string output = args.Length > 0 ? args[0] : Path.Combine(FindRepositoryRoot(), "samples");
Directory.CreateDirectory(output);

foreach (var sample in SampleSet.All)
{
    File.WriteAllBytes(Path.Combine(output, sample.FileName), sample.Content);
    Console.WriteLine($"{sample.FileName,-16} {sample.Content.Length,6} byte  {string.Join(" + ", sample.Sources),-24} {sample.Description}");
}

static string FindRepositoryRoot()
{
    for (var directory = new DirectoryInfo(Environment.CurrentDirectory); directory is not null; directory = directory.Parent)
        if (Directory.Exists(Path.Combine(directory.FullName, ".git"))) return directory.FullName;
    return Environment.CurrentDirectory;
}
