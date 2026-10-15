using Dapper;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MySql.Data.MySqlClient;
using Worker.Domain.Models;
using Worker.Shared;
using Worker.Configuration;

namespace Worker.Infrastructure.Persistence;

public sealed class SensorReadingWriterService : ISensorReadingWriterService
{
    private readonly ILogger<SensorReadingWriterService> _logger;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private volatile bool _ready;

    public SensorReadingWriterService(ILogger<SensorReadingWriterService> logger)
    {
        _logger = logger;
    }

    public async Task WaitUntilReadyAsync(CancellationToken cancellationToken = default)
    {
        if (_ready) return;
        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_ready) return;
            await using var connection = new MySqlConnection(DatabaseConfig.MysqlConnString);
            await DbRetry.OpenWithRetryAsync(connection, _logger, "SensorReadings", cancellationToken);
            try
            {
                // Validate the normalized schema without creating tables or changing master data.
                const string sql = """
                    SELECT readings.id, readings.measured_at, readings.numeric_value,
                           readings.quality_code, readings.raw_payload, readings.ingested_at, readings.uploaded_at,
                           channels.channel_code, channels.channel_name, channels.measurement_name,
                           channels.axis, channels.is_active,
                           units.unit_category, units.unit_symbol, units.unit_name, units.is_deleted,
                           devices.device_code, devices.device_name, devices.status, devices.zone_id,
                           types.sensor_code, types.sensor_name, types.description, types.default_unit_id, types.is_active,
                           assets.asset_code, sites.site_code
                    FROM sensor_readings readings
                    JOIN sensor_channels channels ON channels.id = readings.sensor_channel_id
                    JOIN measurement_units units ON units.id = channels.unit_id
                    JOIN sensor_devices devices ON devices.id = channels.sensor_device_id
                    JOIN sensor_types types ON types.id = devices.sensor_type_id
                    JOIN shms_sites sites ON sites.id = devices.site_id
                    LEFT JOIN structural_assets assets ON assets.id = devices.asset_id
                    LIMIT 0;
                    """;
                await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
                const string keysSql = """
                    SELECT COUNT(DISTINCT TABLE_NAME) FROM (
                        SELECT TABLE_NAME, GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) AS key_columns
                        FROM information_schema.STATISTICS
                        WHERE TABLE_SCHEMA = DATABASE() AND NON_UNIQUE = 0
                        GROUP BY TABLE_NAME, INDEX_NAME
                        HAVING SUM(SUB_PART IS NOT NULL) = 0
                    ) unique_keys
                    WHERE (TABLE_NAME, key_columns) IN (
                        ('measurement_units', 'unit_category,unit_symbol'),
                        ('sensor_types', 'sensor_code'),
                        ('sensor_devices', 'device_code'),
                        ('sensor_channels', 'sensor_device_id,channel_code'),
                        ('sensor_readings', 'sensor_channel_id,measured_at'));
                    """;
                const string referencesSql = """
                    SELECT COUNT(DISTINCT TABLE_NAME, COLUMN_NAME)
                    FROM information_schema.KEY_COLUMN_USAGE
                    WHERE TABLE_SCHEMA = DATABASE() AND REFERENCED_TABLE_SCHEMA = DATABASE()
                      AND REFERENCED_COLUMN_NAME = 'id'
                      AND (TABLE_NAME, COLUMN_NAME, REFERENCED_TABLE_NAME) IN (
                        ('structural_assets', 'site_id', 'shms_sites'),
                        ('sensor_types', 'default_unit_id', 'measurement_units'),
                        ('sensor_devices', 'site_id', 'shms_sites'),
                        ('sensor_devices', 'asset_id', 'structural_assets'),
                        ('sensor_devices', 'zone_id', 'asset_zones'),
                        ('sensor_devices', 'sensor_type_id', 'sensor_types'),
                        ('sensor_channels', 'sensor_device_id', 'sensor_devices'),
                        ('sensor_channels', 'unit_id', 'measurement_units'),
                        ('sensor_readings', 'sensor_channel_id', 'sensor_channels'));
                    """;
                if (await connection.ExecuteScalarAsync<int>(new CommandDefinition(keysSql, cancellationToken: cancellationToken)) != 5
                    || await connection.ExecuteScalarAsync<int>(new CommandDefinition(referencesSql, cancellationToken: cancellationToken)) != 9)
                {
                    throw new InvalidOperationException(
                        "Required SHMS unique indexes or foreign keys are missing. Apply " +
                        "Backend/Web.API.Persistence/Migrations/20261015_001_sensor_integrity_indexes.sql before starting the Worker.");
                }
            }
            catch (MySqlException ex)
            {
                throw new InvalidOperationException(
                    "SHMS schema is missing or incompatible. Apply Backend/database/bajatitian-shms_schema_only.sql " +
                    "for a new database, and migrations 20261015_001_sensor_integrity_indexes.sql and 20261015_002_data_retention.sql for existing databases.", ex);
            }
            _ready = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async Task<long> InsertAsync(ShmsSensorReading reading, CancellationToken cancellationToken = default)
    {
        var ids = await InsertBatchAsync([reading], cancellationToken);
        return ids[0];
    }

    public async Task<IReadOnlyList<long>> InsertBatchAsync(
        IReadOnlyList<ShmsSensorReading> readings, CancellationToken cancellationToken = default, bool uploaded = false)
    {
        if (readings.Count == 0) return Array.Empty<long>();
        await WaitUntilReadyAsync(cancellationToken);
        await using var connection = new MySqlConnection(DatabaseConfig.MysqlConnString);
        await DbRetry.OpenWithRetryAsync(connection, _logger, "SensorReadings", cancellationToken);

        // All channels from one MQTT payload commit together. A retry cannot leave a partial sample.
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var ids = new List<long>(readings.Count);
            foreach (var reading in readings)
            {
                var unitId = await ResolveUnitIdAsync(connection, transaction, reading, cancellationToken);
                var sensorTypeId = await ResolveSensorTypeIdAsync(connection, transaction, reading, unitId, cancellationToken);
                var deviceId = await ResolveSensorDeviceIdAsync(connection, transaction, reading, sensorTypeId, cancellationToken);
                var channelId = await ResolveSensorChannelIdAsync(connection, transaction, reading, deviceId, unitId, cancellationToken);
                var parameters = new
                {
                    sensor_channel_id = channelId,
                    measured_at = reading.MeasuredAt,
                    numeric_value = reading.NumericValue,
                    quality_code = reading.QualityCode,
                    raw_payload = NormalizeRawPayload(reading.RawPayload),
                    uploaded
                };

                const string insertSql = """
                    INSERT INTO sensor_readings
                        (sensor_channel_id, measured_at, numeric_value, quality_code, raw_payload, ingested_at, uploaded_at)
                    VALUES
                        (@sensor_channel_id, @measured_at, @numeric_value, @quality_code, @raw_payload, CURRENT_TIMESTAMP(6), IF(@uploaded, CURRENT_TIMESTAMP(6), NULL))
                    ON DUPLICATE KEY UPDATE
                        uploaded_at = CASE
                            WHEN @uploaded THEN COALESCE(uploaded_at, VALUES(uploaded_at))
                            WHEN NOT (numeric_value <=> VALUES(numeric_value))
                              OR NOT (quality_code <=> VALUES(quality_code))
                              OR NOT (raw_payload <=> VALUES(raw_payload)) THEN NULL
                            ELSE uploaded_at
                        END,
                        numeric_value = VALUES(numeric_value),
                        quality_code = VALUES(quality_code),
                        raw_payload = VALUES(raw_payload),
                        id = LAST_INSERT_ID(id);
                    SELECT LAST_INSERT_ID();
                    """;
                ids.Add(await connection.ExecuteScalarAsync<long>(
                    new CommandDefinition(insertSql, parameters, transaction, cancellationToken: cancellationToken)));
            }

            await transaction.CommitAsync(cancellationToken);
            return ids;
        }
        catch
        {
            // Roll back even if the caller has cancelled the operation.
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static string? NormalizeRawPayload(string? rawPayload)
    {
        if (string.IsNullOrWhiteSpace(rawPayload))
        {
            return null;
        }

        try
        {
            return JToken.Parse(rawPayload).ToString(Formatting.None);
        }
        catch (JsonException)
        {
            return JsonConvert.SerializeObject(rawPayload);
        }
    }

    private static async Task<int> ResolveUnitIdAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        ShmsSensorReading reading,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO measurement_units
                (unit_category, unit_symbol, unit_name, is_deleted)
            VALUES
                (@unit_category, @unit_symbol, @unit_name, 0)
            ON DUPLICATE KEY UPDATE id = LAST_INSERT_ID(id);
            SELECT LAST_INSERT_ID();
            """;

        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new
            {
                unit_category = Clamp(reading.UnitCategory, 50),
                unit_symbol = Clamp(reading.UnitSymbol, 20),
                unit_name = Clamp(reading.UnitSymbol, 80)
            }, transaction, cancellationToken: cancellationToken));
    }

    private static async Task<int> ResolveSensorTypeIdAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        ShmsSensorReading reading,
        int unitId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO sensor_types
                (sensor_code, sensor_name, description, default_unit_id, is_active)
            VALUES
                (@sensor_code, @sensor_name, @description, @default_unit_id, 1)
            ON DUPLICATE KEY UPDATE id = LAST_INSERT_ID(id);
            SELECT LAST_INSERT_ID();
            """;

        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new
            {
                sensor_code = Clamp(reading.SensorCode, 20),
                sensor_name = Clamp($"{reading.SensorCode} Sensor", 100),
                description = Clamp($"{reading.MeasurementName} sensor", 255),
                default_unit_id = unitId
            }, transaction, cancellationToken: cancellationToken));
    }

    private static async Task<long> ResolveSensorDeviceIdAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        ShmsSensorReading reading,
        int sensorTypeId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO sensor_devices
                (site_id, asset_id, zone_id, sensor_type_id, device_code, device_name, status)
            VALUES
                (1, 1, NULL, @sensor_type_id, @device_code, @device_name, 'active')
            ON DUPLICATE KEY UPDATE id = LAST_INSERT_ID(id);
            SELECT id FROM sensor_devices
            WHERE id = LAST_INSERT_ID() AND sensor_type_id = @sensor_type_id;
            """;

        var id = await connection.ExecuteScalarAsync<long?>(
            new CommandDefinition(sql, new
            {
                sensor_type_id = sensorTypeId,
                device_code = Clamp(reading.DeviceCode, 80),
                device_name = Clamp(reading.DeviceCode, 150)
            }, transaction, cancellationToken: cancellationToken));

        return id ?? throw new FormatException($"Device {reading.DeviceCode} is configured for another sensor type.");
    }

    private static async Task<long> ResolveSensorChannelIdAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        ShmsSensorReading reading,
        long deviceId,
        int unitId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO sensor_channels
                (sensor_device_id, unit_id, channel_code, channel_name, measurement_name, axis, is_active)
            VALUES
                (@sensor_device_id, @unit_id, @channel_code, @channel_name, @measurement_name, @axis, 1)
            ON DUPLICATE KEY UPDATE id = LAST_INSERT_ID(id);
            SELECT id FROM sensor_channels
            WHERE id = LAST_INSERT_ID() AND unit_id = @unit_id
              AND measurement_name = @measurement_name
              AND CAST(axis AS BINARY) <=> CAST(@axis AS BINARY);
            """;

        var id = await connection.ExecuteScalarAsync<long?>(
            new CommandDefinition(sql, new
            {
                sensor_device_id = deviceId,
                unit_id = unitId,
                channel_code = Clamp(reading.ChannelCode, 80),
                channel_name = Clamp(reading.ChannelCode, 150),
                measurement_name = Clamp(reading.MeasurementName, 100),
                axis = ClampNullable(reading.Axis, 10)
            }, transaction, cancellationToken: cancellationToken));

        return id ?? throw new FormatException($"Channel {reading.DeviceCode}/{reading.ChannelCode} has a different unit, measurement or axis. Use a separate channel for a different measurement.");
    }

    private static string Clamp(string value, int maxLength) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim()[..Math.Min(value.Trim().Length, maxLength)];

    private static string? ClampNullable(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim()[..Math.Min(value.Trim().Length, maxLength)];
}
