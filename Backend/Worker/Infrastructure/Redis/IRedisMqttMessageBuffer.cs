using Worker.Domain.Models;

namespace Worker.Infrastructure.Redis;

public interface IRedisMqttMessageBuffer
{
    Task EnqueueAsync(BufferedMqttMessage message, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BufferedMqttMessage>> PeekAsync(int maxItems, CancellationToken cancellationToken = default);

    Task RemoveAsync(int count, CancellationToken cancellationToken = default);

    Task<long> CountAsync(CancellationToken cancellationToken = default);
}
