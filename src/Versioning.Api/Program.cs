var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => "Versioning PoC API is running");

app.MapGet("/version", (IHostEnvironment env) =>
{
    var informationalVersion = ThisAssembly.AssemblyInformationalVersion;

    // NBGV embeds commit as "-g{hash}" suffix: "1.0.15-g123abc"
    var commit = "unknown";
    var dashGIndex = informationalVersion.IndexOf("-g", StringComparison.Ordinal);
    if (dashGIndex > 0)
        commit = informationalVersion[(dashGIndex + 2)..];

    // Branch injected by CI (GITHUB_REF_NAME) or local git tooling
    var branch = Environment.GetEnvironmentVariable("GITHUB_REF_NAME")
              ?? Environment.GetEnvironmentVariable("GIT_BRANCH")
              ?? "local";

    return Results.Ok(new
    {
        version = informationalVersion,
        environment = env.EnvironmentName,
        branch,
        commit,
        buildDate = DateTime.UtcNow
    });
});

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }
