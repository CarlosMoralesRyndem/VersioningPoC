var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => "Versioning PoC API is running");

app.MapGet("/version", (IHostEnvironment env) =>
    Results.Ok(new
    {
        version = ThisAssembly.AssemblyInformationalVersion,
        environment = env.EnvironmentName,
        branch = BuildInfo.Branch,
        commit = BuildInfo.Commit,
        buildDate = DateTime.UtcNow
    }));

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }
