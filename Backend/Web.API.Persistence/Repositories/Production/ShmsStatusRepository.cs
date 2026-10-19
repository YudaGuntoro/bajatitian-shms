using Microsoft.EntityFrameworkCore;
using Web.API.Domain.Production;
using Web.API.Persistence.Context;

namespace Web.API.Persistence.Repositories.Production;

public sealed class ShmsStatusRepository : IShmsStatusRepository
{
    private readonly AppDbContext _context;

    public ShmsStatusRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task EnsureServerSyncStatusTableAsync(CancellationToken cancellationToken = default) =>
        _context.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS server_sync_status (
    id INT PRIMARY KEY,
    server_name VARCHAR(100) NOT NULL,
    endpoint_url VARCHAR(500) NULL,
    is_online TINYINT(1) NOT NULL DEFAULT 0,
    outage_started_at DATETIME(6) NULL,
    last_success_at DATETIME(6) NULL,
    last_failure_at DATETIME(6) NULL,
    last_error TEXT NULL,
    redis_buffer_count BIGINT NOT NULL DEFAULT 0,
    db_spillover_count BIGINT NOT NULL DEFAULT 0,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6)
) ENGINE=InnoDB;", cancellationToken);

    public Task<DateTime?> GetLastMqttAtAsync(CancellationToken cancellationToken = default) =>
        _context.LogBuffers
            .AsNoTracking()
            .OrderByDescending(x => x.TimeStamp)
            .Select(x => (DateTime?)x.TimeStamp)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<ServerSyncStatus?> GetServerSyncStatusAsync(CancellationToken cancellationToken = default) =>
        _context.ServerSyncStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);

    public async Task<IReadOnlyList<LatestSensorReadingResponse>> GetLatestReadingsAsync(CancellationToken cancellationToken = default) =>
        await _context.Database.SqlQueryRaw<LatestSensorReadingResponse>(LatestReadingsSql).ToListAsync(cancellationToken);

    // Each correlated lookup uses an existing channel/time or device/time index.
    // Do not derive sensor state from the dashboard's paginated recent activity.
    public const string LatestReadingsSql = """
        SELECT devices.device_code AS DeviceCode, channels.channel_code AS ChannelCode,
               channels.measurement_name AS MeasurementName, channels.axis AS Axis,
               readings.numeric_value AS Value, units.unit_symbol AS Unit,
               readings.measured_at AS MeasuredAt,
               COALESCE(recent_log.time_stamp, readings.ingested_at) AS ReceivedAt,
               GREATEST(0, TIMESTAMPDIFF(SECOND, COALESCE(recent_log.time_stamp, readings.ingested_at), CURRENT_TIMESTAMP(6))) AS ReceivedAgeSeconds,
               recent_log.topic AS Topic
        FROM sensor_channels channels
        JOIN sensor_devices devices ON devices.id = channels.sensor_device_id
        JOIN measurement_units units ON units.id = channels.unit_id
        JOIN sensor_readings readings ON readings.id = (
            SELECT latest.id FROM sensor_readings latest
            WHERE latest.sensor_channel_id = channels.id
            ORDER BY latest.measured_at DESC LIMIT 1
        )
        LEFT JOIN log_buffer recent_log ON recent_log.id = (
            SELECT latest_log.id FROM log_buffer latest_log
            WHERE latest_log.device_id = devices.device_code
            ORDER BY latest_log.time_stamp DESC, latest_log.id DESC LIMIT 1
        )
        ORDER BY devices.device_code, channels.channel_code
        """;
}
