public class VersionLoggerService(ILogger<VersionLoggerService> logger, IHostEnvironment env)
    : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Worker started | Version={Version} | Branch={Branch} | Commit={Commit} | Environment={Environment}",
            BuildInfo.Version, BuildInfo.Branch, BuildInfo.Commit, env.EnvironmentName);

        return Task.CompletedTask;
    }
}
