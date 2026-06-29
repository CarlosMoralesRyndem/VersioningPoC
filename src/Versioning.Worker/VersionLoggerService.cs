public class VersionLoggerService(ILogger<VersionLoggerService> logger, IHostEnvironment env)
    : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var version = ThisAssembly.AssemblyInformationalVersion;

        var commit = "unknown";
        var dashGIndex = version.IndexOf("-g", StringComparison.Ordinal);
        if (dashGIndex > 0)
            commit = version[(dashGIndex + 2)..];

        var branch = Environment.GetEnvironmentVariable("GITHUB_REF_NAME")
                  ?? Environment.GetEnvironmentVariable("GIT_BRANCH")
                  ?? "local";

        logger.LogInformation(
            "Worker started | Version={Version} | Branch={Branch} | Commit={Commit} | Environment={Environment}",
            version, branch, commit, env.EnvironmentName);

        return Task.CompletedTask;
    }
}
