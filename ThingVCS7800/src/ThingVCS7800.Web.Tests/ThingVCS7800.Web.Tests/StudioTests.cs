using ThingVCS7800.Web;
using ThingVCS;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
namespace ThingVCS7800.Web.Tests;
public class StudioTests
{
    [Fact] public void TemplateBuildsParsableCartridgeAndExecutesOnBoard()
    {
        var image=new StudioCompiler().Build(StudioCompiler.Template,"Test game");
        var parsed=CartridgeImage.Parse(image,true); Assert.Equal(16384,parsed.Size);
        using var session=new WebSession(); session.Load(image,"Test",false); session.Step();
        Assert.Equal(320,session.Width); Assert.Equal(242,session.Height); Assert.Equal(320*242*4,session.Frame.Length);
        Assert.Equal(15,((TiaInputs)session.Computer.Board.TiaInputSocket.Chip!).Audio.RegisterValue(0x19));
    }
    [Theory][InlineData("!source \"secret\"")][InlineData(".include \"secret\"")][InlineData("!binary \"secret\"")][InlineData("!for x,0,999999 {}")]
    public void SingleFileEditorRejectsFileAccessAndLoops(string source)=>Assert.Throws<ArgumentException>(()=>new StudioCompiler().Build(source,"test"));
    [Fact] public void InvalidBuildAndLoadPreserveExistingCartridge()
    {
        using var session=new WebSession();session.Demo(); var previous=session.Computer.Board.CartridgeSocket.Chip;
        Assert.ThrowsAny<Exception>(()=>new StudioCompiler().Build("*=$1000\nnop","bad"));
        Assert.ThrowsAny<Exception>(()=>session.Load(new byte[3],"bad",false));
        Assert.Same(previous,session.Computer.Board.CartridgeSocket.Chip);
    }
    [Fact] public void SeparateWebSessionsHaveIndependentBoards()
    {
        using var first=new WebSession();using var second=new WebSession();first.Demo();
        Assert.False(second.Computer.HasCartridge);first.Computer.Board.Write(0x1800,42);
        Assert.NotEqual(first.Computer.Board.Read(0x1800),second.Computer.Board.Read(0x1800));
    }
    [Fact] public void StudioComponentBuildButtonLoadsTemplate()
    {
        using var context=new BunitContext();context.Services.AddScoped<WebSession>();context.Services.AddScoped<StudioCompiler>();
        context.JSInterop.Mode=JSRuntimeMode.Loose;
        var module=context.JSInterop.SetupModule("./_content/ThingVCS7800.Web/monitor.js");
        module.SetupModule("create",_=>true);
        var component=context.Render<Workbench>(p=>p.Add(x=>x.Studio,true));
        component.FindAll("button").Single(b=>b.TextContent=="Build & load").Click();
        Assert.True(context.Services.GetRequiredService<WebSession>().Computer.HasCartridge);
        Assert.Empty(component.FindAll("[role=alert]"));
    }
    [Fact] public void ComponentShowsBuildErrorsWithoutLoadingCartridge()
    {
        using var context=new BunitContext();context.Services.AddScoped<WebSession>();context.Services.AddScoped<StudioCompiler>();
        context.JSInterop.Mode=JSRuntimeMode.Loose;
        var module=context.JSInterop.SetupModule("./_content/ThingVCS7800.Web/monitor.js");module.SetupModule("create",_=>true);
        var component=context.Render<Workbench>(p=>p.Add(x=>x.Studio,true));
        component.Find("textarea").Change("*=$1000\nnop");
        component.FindAll("button").Single(b=>b.TextContent=="Build & load").Click();
        Assert.Single(component.FindAll("[role=alert]"));
        Assert.False(context.Services.GetRequiredService<WebSession>().Computer.HasCartridge);
    }
    [Fact] public void WebComputerHasMonitorAndNoStudioEditor()
    {
        using var context=new BunitContext();context.Services.AddScoped<WebSession>();context.Services.AddScoped<StudioCompiler>();
        context.JSInterop.Mode=JSRuntimeMode.Loose;
        var module=context.JSInterop.SetupModule("./_content/ThingVCS7800.Web/monitor.js");module.SetupModule("create",_=>true);
        var component=context.Render<Workbench>();
        Assert.Single(component.FindAll("canvas"));Assert.Empty(component.FindAll("textarea"));
    }
}
