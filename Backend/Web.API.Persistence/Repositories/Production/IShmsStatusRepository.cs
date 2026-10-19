using Web.API.Domain.Production;

namespace Web.API.Persistence.Repositories.Production;

public interface IShmsStatusRepository
{
    Task EnsureServerSyncStatusTableAsync(CancellationToken cancellationToken = default);

    Task<DateTime?> GetLastMqttAtAsync(CancellationToken cancellationToken = default);

    Task<ServerSyncStatus?> GetServerSyncStatusAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LatestSensorReadingResponse>> GetLatestReadingsAsync(CancellationToken cancellationToken = default);
}
