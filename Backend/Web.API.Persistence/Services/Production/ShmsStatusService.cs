using Web.API.Domain.Production;
using Web.API.Persistence.Repositories.Production;

namespace Web.API.Persistence.Services.Production;

public sealed class ShmsStatusService : IShmsStatusService
{
    private readonly IShmsStatusRepository _repository;

    public ShmsStatusService(IShmsStatusRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<LatestSensorReadingResponse>> GetLatestReadingsAsync(CancellationToken cancellationToken = default) =>
        _repository.GetLatestReadingsAsync(cancellationToken);

    public async Task<ShmsSystemStatusResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        var lastMqttAt = await _repository.GetLastMqttAtAsync(cancellationToken);
        await _repository.EnsureServerSyncStatusTableAsync(cancellationToken);
        var syncStatus = await _repository.GetServerSyncStatusAsync(cancellationToken);
        var downtimeSeconds = syncStatus is { IsOnline: false, OutageStartedAt: not null }
            ? Math.Max(0, (int)(DateTime.Now - syncStatus.OutageStartedAt.Value).TotalSeconds)
            : 0;

        return new ShmsSystemStatusResponse
        {
            Status = "online",
            CheckedAt = DateTime.Now,
            LastMqttAt = lastMqttAt,
            MainServer = syncStatus is null
                ? new MainServerStatusResponse()
                : new MainServerStatusResponse
                {
                    Configured = !string.IsNullOrWhiteSpace(syncStatus.EndpointUrl),
                    ServerName = syncStatus.ServerName,
                    EndpointUrl = syncStatus.EndpointUrl,
                    Online = syncStatus.IsOnline,
                    OutageStartedAt = syncStatus.OutageStartedAt,
                    DowntimeSeconds = downtimeSeconds,
                    LastSuccessAt = syncStatus.LastSuccessAt,
                    LastFailureAt = syncStatus.LastFailureAt,
                    LastError = syncStatus.LastError,
                    RedisBufferCount = syncStatus.RedisBufferCount,
                    DbSpilloverCount = syncStatus.DbSpilloverCount
                }
        };
    }
}
