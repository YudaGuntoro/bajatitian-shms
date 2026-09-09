using Web.API.Domain.Production;

namespace Web.API.Persistence.Repositories.Production;

public interface IMqttConfigurationRepository
{
    Task EnsureTableAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MqttSensorTopicConfig>> GetTopicsAsync(CancellationToken cancellationToken = default);

    Task UpsertTopicsAsync(
        IReadOnlyCollection<MqttSensorTopicConfig> topics,
        CancellationToken cancellationToken = default);
}
