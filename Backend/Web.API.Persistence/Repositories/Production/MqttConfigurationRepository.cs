using Microsoft.EntityFrameworkCore;
using Web.API.Domain.Production;
using Web.API.Persistence.Context;

namespace Web.API.Persistence.Repositories.Production;

public sealed class MqttConfigurationRepository : IMqttConfigurationRepository
{
    private readonly AppDbContext _context;

    public MqttConfigurationRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task EnsureTableAsync(CancellationToken cancellationToken = default)
    {
        await _context.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS mqtt_broker_configs (
    id INT AUTO_INCREMENT PRIMARY KEY,
    config_name VARCHAR(100) NOT NULL,
    host VARCHAR(255) NOT NULL,
    port INT NOT NULL DEFAULT 1883,
    client_id VARCHAR(100) NOT NULL,
    username VARCHAR(100) NULL,
    password_secret_name VARCHAR(150) NULL,
    use_tls TINYINT(1) NOT NULL DEFAULT 0,
    is_active TINYINT(1) NOT NULL DEFAULT 1,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_mqtt_broker_configs_config_name (config_name),
    INDEX ix_mqtt_broker_configs_is_active (is_active)
);

CREATE TABLE IF NOT EXISTS mqtt_sensor_topics (
    id INT AUTO_INCREMENT PRIMARY KEY,
    code VARCHAR(20) NOT NULL,
    name VARCHAR(100) NOT NULL,
    topic VARCHAR(255) NOT NULL,
    qos INT NOT NULL DEFAULT 1,
    enabled TINYINT(1) NOT NULL DEFAULT 1,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_mqtt_sensor_topics_code (code),
    INDEX ix_mqtt_sensor_topics_enabled (enabled)
);

UPDATE mqtt_sensor_topics legacy
LEFT JOIN mqtt_sensor_topics slot ON slot.code = 'TILT01'
SET legacy.code = 'TILT01', legacy.name = 'Tilt Sensor 01'
WHERE legacy.code = 'TILT' AND slot.id IS NULL;

UPDATE mqtt_sensor_topics legacy
LEFT JOIN mqtt_sensor_topics slot ON slot.code = 'VW01'
SET legacy.code = 'VW01', legacy.name = 'Vibrating Wire Sensor 01'
WHERE legacy.code = 'VW' AND slot.id IS NULL;

UPDATE mqtt_sensor_topics legacy
LEFT JOIN mqtt_sensor_topics slot ON slot.code = 'ATRH01'
SET legacy.code = 'ATRH01', legacy.name = 'Air Temperature & RH Sensor 01'
WHERE legacy.code = 'ATRH' AND slot.id IS NULL;

UPDATE mqtt_sensor_topics legacy
LEFT JOIN mqtt_sensor_topics slot ON slot.code = 'ACC01'
SET legacy.code = 'ACC01', legacy.name = 'Accelerometer Sensor 01'
WHERE legacy.code = 'ACC' AND slot.id IS NULL;
", cancellationToken);
    }

    public async Task<MqttBrokerConfig?> GetActiveBrokerConfigAsync(CancellationToken cancellationToken = default)
    {
        return await _context.MqttBrokerConfigs
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MqttSensorTopicConfig>> GetTopicsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.MqttSensorTopicConfigs
            .AsNoTracking()
            .OrderBy(x => x.Code.StartsWith("TILT") ? 1 : x.Code.StartsWith("VW") ? 2 : x.Code.StartsWith("ATRH") ? 3 : x.Code.StartsWith("ACC") ? 4 : 5)
            .ThenBy(x => x.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task UpsertBrokerConfigAsync(MqttBrokerConfig brokerConfig, CancellationToken cancellationToken = default)
    {
        var configName = string.IsNullOrWhiteSpace(brokerConfig.ConfigName)
            ? "default"
            : brokerConfig.ConfigName.Trim();

        var existing = await _context.MqttBrokerConfigs
            .FirstOrDefaultAsync(x => x.ConfigName == configName, cancellationToken);

        if (existing is null)
        {
            existing = new MqttBrokerConfig
            {
                ConfigName = configName,
                CreatedAt = DateTime.Now
            };
            _context.MqttBrokerConfigs.Add(existing);
        }

        existing.Host = brokerConfig.Host;
        existing.Port = brokerConfig.Port;
        existing.ClientId = brokerConfig.ClientId;
        existing.Username = brokerConfig.Username;
        existing.PasswordSecretName = brokerConfig.PasswordSecretName;
        existing.UseTls = brokerConfig.UseTls;
        existing.IsActive = true;
        existing.UpdatedAt = DateTime.Now;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpsertTopicsAsync(
        IReadOnlyCollection<MqttSensorTopicConfig> topics,
        CancellationToken cancellationToken = default)
    {
        foreach (var item in topics)
        {
            var existing = await _context.MqttSensorTopicConfigs
                .FirstOrDefaultAsync(x => x.Code == item.Code, cancellationToken);

            if (existing is null)
            {
                existing = new MqttSensorTopicConfig
                {
                    Code = item.Code,
                    CreatedAt = DateTime.Now
                };
                _context.MqttSensorTopicConfigs.Add(existing);
            }

            existing.Name = item.Name;
            existing.Topic = item.Topic;
            existing.Qos = item.Qos;
            existing.Enabled = item.Enabled;
            existing.UpdatedAt = DateTime.Now;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
