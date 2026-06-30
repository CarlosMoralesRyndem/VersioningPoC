public class VersionLoggerService(ILogger<VersionLoggerService> logger)
    : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Worker started | Version={Version} | Branch={Branch} | Commit={Commit}",
            BuildInfo.Version, BuildInfo.Branch, BuildInfo.Commit);

        return Task.CompletedTask;
    }
}
