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
)", cancellationToken);
    }

    public async Task<IReadOnlyList<MqttSensorTopicConfig>> GetTopicsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.MqttSensorTopicConfigs
            .AsNoTracking()
            .OrderBy(x => x.Code == "TILT" ? 1 : x.Code == "VW" ? 2 : x.Code == "ATRH" ? 3 : x.Code == "ACC" ? 4 : 5)
            .ThenBy(x => x.Code)
            .ToListAsync(cancellationToken);
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
