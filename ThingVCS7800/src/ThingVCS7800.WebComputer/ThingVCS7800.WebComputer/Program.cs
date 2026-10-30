using ThingVCS7800.WebComputer.Components;
using ThingVCS7800.Web;
var builder=WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddScoped<WebSession>(); builder.Services.AddScoped<StudioCompiler>();
var app=builder.Build();
app.UseAntiforgery(); app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
