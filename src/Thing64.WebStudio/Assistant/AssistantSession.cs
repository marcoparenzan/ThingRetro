using RalfAI;

namespace Thing64.WebStudio.Assistant;

// Where RalfAI's configuration is (Assistant:ConfigPath, the config.json of RalfAI with providers and keys) and which
// model it uses for the studio (Assistant:ContextKey, looked up in AzureAIFoundry:ContextModels)
public sealed class AssistantSettings(IConfiguration configuration)
{
    public string ConfigPath { get; } = configuration["Assistant:ConfigPath"] ?? "";
    public string ContextKey { get; } = configuration["Assistant:ContextKey"] ?? "thing64";
    public bool Available => File.Exists(ConfigPath);
}

// One conversation with RalfAI about one project. The engine keeps the history in the project folder
// (.ralfai/session.md, with the costs next to it), so the conversation goes on when the project is opened again
public sealed class AssistantSession
{
    public required AgentEngine Engine { get; init; }

    public static async Task<AssistantSession> StartAsync(AssistantSettings settings, Project project, IStudioHost host, CancellationToken cancel)
    {
        // The studio runs on a server: no voice tools (they would speak and listen on the server's devices)
        var configuration = new ConfigurationBuilder()
            .AddConfiguration(RalfAIConfig.Load(settings.ConfigPath))
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AzureSpeech:ApiKey"] = "" })
            .Build();
        var engine = await AgentEngine.CreateAsync(configuration, project.Folder, settings.ContextKey,
            new Thing64AgentContext(project, host), cancel);
        return new AssistantSession { Engine = engine };
    }
}
