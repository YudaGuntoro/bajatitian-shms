using System.Globalization;
using Web.API.Domain.Production;
using Web.API.Persistence.Repositories.Production;

namespace Web.API.Persistence.Services.Production;

public sealed class MqttConfigurationService : IMqttConfigurationService
{
    private static readonly MqttSensorTopicConfig[] DefaultTopics = BuildDefaultTopics();
    private static readonly HashSet<string> DefaultTopicCodes = DefaultTopics
        .Select(topic => topic.Code)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static readonly MqttBrokerConfig DefaultBrokerConfig = new()
    {
        ConfigName = "default",
        Host = "localhost",
        Port = 1883,
        ClientId = "SHMSClient",
        UseTls = false,
        IsActive = true
    };

    private readonly IMqttConfigurationRepository _repository;

    public MqttConfigurationService(IMqttConfigurationRepository repository)
    {
        _repository = repository;
    }

    public async Task<MqttConfigurationResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(cancellationToken);
        return BuildResponse(
            await _repository.GetActiveBrokerConfigAsync(cancellationToken) ?? DefaultBrokerConfig,
            await _repository.GetTopicsAsync(cancellationToken));
    }

    public async Task<MqttConfigurationResponse> UpdateAsync(
        UpdateMqttConfigurationRequest request,
        CancellationToken cancellationToken = default)
    {
        await _repository.EnsureTableAsync(cancellationToken);
        var existingBroker = await _repository.GetActiveBrokerConfigAsync(cancellationToken) ?? DefaultBrokerConfig;
        await _repository.UpsertBrokerConfigAsync(NormalizeBroker(request, existingBroker), cancellationToken);
        await _repository.UpsertTopicsAsync(NormalizeTopics(request.Topics), cancellationToken);
        return BuildResponse(
            await _repository.GetActiveBrokerConfigAsync(cancellationToken) ?? DefaultBrokerConfig,
            await _repository.GetTopicsAsync(cancellationToken));
    }

    private async Task EnsureDefaultsAsync(CancellationToken cancellationToken)
    {
        await _repository.EnsureTableAsync(cancellationToken);
        var brokerConfig = await _repository.GetActiveBrokerConfigAsync(cancellationToken);
        if (brokerConfig is null)
        {
            await _repository.UpsertBrokerConfigAsync(DefaultBrokerConfig, cancellationToken);
        }

        var existing = await _repository.GetTopicsAsync(cancellationToken);
        var missingDefaults = DefaultTopics
            .Where(defaultTopic => existing.All(item => item.Code != defaultTopic.Code))
            .Select(Clone)
            .ToList();

        if (missingDefaults.Count > 0)
        {
            await _repository.UpsertTopicsAsync(missingDefaults, cancellationToken);
        }
    }

    private static MqttConfigurationResponse BuildResponse(
        MqttBrokerConfig brokerConfig,
        IReadOnlyList<MqttSensorTopicConfig> topics) =>
        new()
        {
            BrokerHost = brokerConfig.Host,
            BrokerPort = brokerConfig.Port.ToString(CultureInfo.InvariantCulture),
            ClientId = brokerConfig.ClientId,
            Username = brokerConfig.Username,
            UseTls = brokerConfig.UseTls,
            Topics = topics
                .Where(topic => DefaultTopicCodes.Contains(topic.Code))
                .ToList()
        };

    private static MqttBrokerConfig NormalizeBroker(UpdateMqttConfigurationRequest request, MqttBrokerConfig current)
    {
        var host = string.IsNullOrWhiteSpace(request.BrokerHost)
            ? current.Host
            : TrimTo(request.BrokerHost, 255);

        var port = int.TryParse(request.BrokerPort, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPort)
            ? parsedPort
            : current.Port;

        return new MqttBrokerConfig
        {
            ConfigName = "default",
            Host = host,
            Port = Math.Clamp(port, 1, 65535),
            ClientId = string.IsNullOrWhiteSpace(request.ClientId)
                ? current.ClientId
                : TrimTo(request.ClientId, 100),
            Username = string.IsNullOrWhiteSpace(request.Username) ? current.Username : TrimTo(request.Username, 100),
            PasswordSecretName = current.PasswordSecretName,
            UseTls = request.UseTls ?? current.UseTls,
            IsActive = true
        };
    }

    private static IReadOnlyList<MqttSensorTopicConfig> NormalizeTopics(IEnumerable<MqttSensorTopicConfig>? topics)
    {
        var requested = topics?
            .Where(x => !string.IsNullOrWhiteSpace(x.Code))
            .ToDictionary(x => x.Code.Trim().ToUpperInvariant(), x => x, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, MqttSensorTopicConfig>(StringComparer.OrdinalIgnoreCase);

        return DefaultTopics.Select(defaultTopic =>
        {
            requested.TryGetValue(defaultTopic.Code, out var topic);
            var normalizedTopic = topic?.Topic?.Trim();

            return new MqttSensorTopicConfig
            {
                Code = defaultTopic.Code,
                Name = string.IsNullOrWhiteSpace(topic?.Name) ? defaultTopic.Name : TrimTo(topic.Name, 100),
                Topic = string.IsNullOrWhiteSpace(normalizedTopic) ? defaultTopic.Topic : TrimTo(normalizedTopic, 255),
                Qos = Math.Clamp(topic?.Qos ?? defaultTopic.Qos, 0, 2),
                Enabled = topic?.Enabled ?? defaultTopic.Enabled
            };
        }).ToList();
    }

    private static MqttSensorTopicConfig Clone(MqttSensorTopicConfig source) =>
        new()
        {
            Code = source.Code,
            Name = source.Name,
            Topic = source.Topic,
            Qos = source.Qos,
            Enabled = source.Enabled
        };

    private static string TrimTo(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static MqttSensorTopicConfig[] BuildDefaultTopics()
    {
        var sensorTypes = new[]
        {
            new { Code = "TILT", Name = "Tilt Sensor", Slug = "tilt" },
            new { Code = "VW", Name = "Vibrating Wire Sensor", Slug = "vw" },
            new { Code = "ATRH", Name = "Air Temperature & RH Sensor", Slug = "atrh" },
            new { Code = "ACC", Name = "Accelerometer Sensor", Slug = "acc" }
        };

        return sensorTypes
            .SelectMany(sensor => Enumerable.Range(1, 9).Select(slot =>
            {
                var suffix = slot.ToString("00", CultureInfo.InvariantCulture);
                return new MqttSensorTopicConfig
                {
                    Code = $"{sensor.Code}{suffix}",
                    Name = $"{sensor.Name} {suffix}",
                    Topic = $"adit/rnd/test123/{sensor.Slug}{suffix}",
                    Qos = 1,
                    Enabled = true
                };
            }))
            .ToArray();
    }
}
