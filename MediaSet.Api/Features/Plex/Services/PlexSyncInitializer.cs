using MediaSet.Api.Features.Plex.Models;

namespace MediaSet.Api.Features.Plex.Services;

/// <summary>
/// Runs before the app serves requests: creates the PlexSyncRuns collection and index, and closes
/// runs a restart left in progress as interrupted. A failure is logged and does not stop the app,
/// because MongoDB may only be briefly unavailable.
/// </summary>
public class PlexSyncInitializer(IPlexSyncRunStore store, ILogger<PlexSyncInitializer> logger) : IHostedService
{
    public const string InterruptedMessage = "The application restarted while the run was in progress.";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await store.EnsureCreatedAsync(cancellationToken);
            var closed = await store.InterruptInProgressAsync(InterruptedMessage, DateTime.UtcNow, cancellationToken);
            if (closed > 0)
            {
                logger.LogWarning("Closed {Count} Plex sync run(s) left in progress as {State}", closed, PlexSyncRunStates.Interrupted);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not prepare the PlexSyncRuns collection at startup");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
