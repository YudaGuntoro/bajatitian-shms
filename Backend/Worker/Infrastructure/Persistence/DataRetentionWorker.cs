using SHMS.Storage;

namespace Worker.Infrastructure.Persistence;

public sealed class DataRetentionWorker(DataRetentionStore store, ILogger<DataRetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try
            {
                if (await store.RunIfDueAsync(stoppingToken))
                    logger.LogInformation("[Retention] FIFO cleanup completed.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "[Retention] Cleanup failed; will retry on the next scheduled check."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
