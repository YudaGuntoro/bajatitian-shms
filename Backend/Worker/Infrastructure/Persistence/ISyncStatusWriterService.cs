namespace Worker.Infrastructure.Persistence;

public interface ISyncStatusWriterService
{
    Task MarkSuccessAsync(string? endpointUrl, long redisBufferCount, CancellationToken cancellationToken = default);

    Task MarkFailureAsync(string? endpointUrl, string error, long redisBufferCount, CancellationToken cancellationToken = default);

    Task MarkDatabaseFallbackAsync(string? endpointUrl, long redisBufferCount, int movedCount, CancellationToken cancellationToken = default);
}
