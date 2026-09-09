using Worker.Domain.Models;

namespace Worker.Infrastructure.Persistence;

public interface ISensorReadingWriterService
{
    Task WaitUntilReadyAsync(CancellationToken cancellationToken = default);

    Task<long> InsertAsync(ShmsSensorReading reading, CancellationToken cancellationToken = default);
}
