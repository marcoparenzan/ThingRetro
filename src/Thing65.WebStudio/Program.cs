using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Thing65.WebStudio;
using Thing65.WebStudio.Components;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents()
    .AddHubOptions(options => options.MaximumReceiveMessageSize = 4 * 1024 * 1024);   // whole sources saved at once
builder.Services.AddSingleton<ProjectStore>();
builder.Services.AddSingleton<Thing65.WebStudio.Assistant.AssistantSettings>();

// The C64: the published web computer (dotnet publish src/Thing65.WebComputer -c Release -o artifacts/web),
// served at /computer/ from the same origin, so that the studio can send it programs (postMessage) and both share
// the ROMs the browser keeps
var computer = new WebComputerSite(Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath,
    builder.Configuration["Studio:ComputerFolder"] ?? "../../artifacts/web/wwwroot")));
builder.Services.AddSingleton(computer);

var app = builder.Build();
var types = new FileExtensionContentTypeProvider();
types.Mappings[".dat"] = "application/octet-stream";
if (computer.Available)
{
    var files = new PhysicalFileProvider(computer.Folder);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files, RequestPath = "/computer" });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = files,
        RequestPath = "/computer",
        ContentTypeProvider = types,
        // computer.js and the boot manifest have no hash in their names: without this a browser keeps the C64 it
        // had, and a newly published one (a new message, a new capture) never reaches it. no-cache still allows a 304
        OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache",
    });
}
else
{
    app.Logger.LogWarning("No web computer in {Folder}: publish it with dotnet publish src/Thing65.WebComputer -c Release -o artifacts/web", computer.Folder);
}

app.UseAntiforgery();
app.MapStaticAssets();

// The files of a project, for the pages that show them (the pictures of the graphics editor)
app.MapGet("/project-files/{project}/{**path}", (string project, string path, ProjectStore store) =>
{
    try
    {
        string full = store.Open(project).FullPath(path);
        if (!File.Exists(full)) return Results.NotFound();
        return Results.File(full, types.TryGetContentType(full, out var type) ? type : "application/octet-stream");
    }
    catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException)
    {
        return Results.NotFound();
    }
});

// The embedded files (see the csproj): the resource wwwroot/js/asm6502.js is served at /js/asm6502.js
var assembly = typeof(Program).Assembly;
foreach (string resource in assembly.GetManifestResourceNames().Where(name => name.StartsWith("wwwroot/")))
{
    string url = resource["wwwroot".Length..];
    string type = types.TryGetContentType(url, out var known) ? known : "application/octet-stream";
    app.MapGet(url, () => Results.Stream(assembly.GetManifestResourceStream(resource)!, type));
}
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();

