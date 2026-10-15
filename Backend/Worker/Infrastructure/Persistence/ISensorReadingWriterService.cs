using Worker.Domain.Models;

namespace Worker.Infrastructure.Persistence;

public interface ISensorReadingWriterService
{
    Task WaitUntilReadyAsync(CancellationToken cancellationToken = default);

    Task<long> InsertAsync(ShmsSensorReading reading, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<long>> InsertBatchAsync(IReadOnlyList<ShmsSensorReading> readings, CancellationToken cancellationToken = default, bool uploaded = false);
}
