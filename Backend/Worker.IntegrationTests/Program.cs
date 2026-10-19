using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using MySql.Data.MySqlClient;
using Worker.Application.Mapping;
using Worker.Domain.Models;
using Worker.Infrastructure.Persistence;

// This executable uses a real MySQL 8 instance and owns only its generated test database.
var connectionString = Environment.GetEnvironmentVariable("SHMS_TEST_CONNECTION_STRING")
    ?? throw new InvalidOperationException("Set SHMS_TEST_CONNECTION_STRING to a MySQL 8 connection with CREATE DATABASE permission.");
var repository = new DirectoryInfo(Directory.GetCurrentDirectory());
while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "Backend/database/bajatitian-shms_schema_only.sql")))
    repository = repository.Parent;
if (repository is null) throw new InvalidOperationException("Run from inside the SHMS repository.");

var database = $"shms_test_ingestion_{Guid.NewGuid():N}";
var builder = new MySqlConnectionStringBuilder(connectionString) { Database = "", AllowUserVariables = true };
await using var admin = new MySqlConnection(builder.ConnectionString);
await admin.OpenAsync();
await admin.ExecuteAsync($"CREATE DATABASE `{database}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;");
var originalConnection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
try
{
    builder.Database = database;
    Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", builder.ConnectionString);
    await using var db = new MySqlConnection(builder.ConnectionString);
    await db.OpenAsync();
    var schema = await File.ReadAllTextAsync(Path.Combine(repository.FullName, "Backend/database/bajatitian-shms_schema_only.sql"));
    await db.ExecuteAsync(schema.Replace("`bajatitian_shms`", $"`{database}`"));
    var migration = await File.ReadAllTextAsync(Path.Combine(repository.FullName,
        "Backend/Web.API.Persistence/Migrations/20261015_001_sensor_integrity_indexes.sql"));

    // Exercise the upgrade path from older Worker-created tables, not just a fresh schema.
    await db.ExecuteAsync("ALTER TABLE log_buffer DROP FOREIGN KEY fk_log_buffer_sensor_device; ALTER TABLE log_buffer DROP INDEX ix_log_buffer_sensor_device_time_stamp;");
    await db.ExecuteAsync("ALTER TABLE sensor_readings DROP FOREIGN KEY fk_sensor_readings_channel; ALTER TABLE sensor_readings DROP INDEX uq_sensor_readings_channel_measured_at;");
    var rejectedSchema = false;
    try { await new SensorReadingWriterService(NullLogger<SensorReadingWriterService>.Instance).WaitUntilReadyAsync(); }
    catch (InvalidOperationException) { rejectedSchema = true; }
    Check(rejectedSchema, "Worker refuses ingestion when required indexes or foreign keys are missing");
    await db.ExecuteAsync(migration);
    var indexes = await CountIndexes();
    var tables = await CountTables();
    await db.ExecuteAsync(migration);
    Check(await CountIndexes() == indexes, "migration is repeatable without duplicate indexes");
    Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM information_schema.KEY_COLUMN_USAGE WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='sensor_readings' AND REFERENCED_TABLE_NAME='sensor_channels'") == 1,
        "migration restores reading/channel foreign key");
    Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM information_schema.KEY_COLUMN_USAGE WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='log_buffer' AND REFERENCED_TABLE_NAME='sensor_devices'") == 1,
        "migration restores log/device foreign key");

    await db.ExecuteAsync("INSERT INTO shms_sites(id,site_code,site_name) VALUES(1,'TEST','Integration Test'); INSERT INTO structural_assets(id,site_id,asset_code,asset_name,asset_type) VALUES(1,1,'TEST','Test Bridge','bridge');");
    var writer = new SensorReadingWriterService(NullLogger<SensorReadingWriterService>.Instance);
    await Task.WhenAll(writer.WaitUntilReadyAsync(), writer.WaitUntilReadyAsync());
    const string payload = "2026-10-15 10:00:00,1.1,2.2,20,21,100,200";
    var readings = ShmsPayloadMapper.ToSensorReadings("test/tilt01", payload)
        .Concat(ShmsPayloadMapper.ToSensorReadings("test/tilt02", payload)).ToArray();
    var ids = await writer.InsertBatchAsync(readings);
    Check(ids.Count == 12 && ids.All(id => id > 0), "multiple devices and channels persist together");
    Check(await Count("sensor_readings") == 12 && await Count("sensor_channels") == 12
        && await Count("sensor_devices") == 2 && await Count("sensor_types") == 1
        && await Count("measurement_units") == 3, "devices, channels, types and units remain normalized");
    try
    {
        await writer.InsertAsync(new ShmsSensorReading
        {
            DeviceCode = readings[0].DeviceCode, SensorCode = "TILT", ChannelCode = readings[0].ChannelCode,
            MeasurementName = "tilt", Axis = "X", UnitSymbol = "rad", UnitCategory = "angle",
            NumericValue = 1, MeasuredAt = readings[0].MeasuredAt.AddMinutes(1), QualityCode = "good"
        });
        throw new InvalidOperationException("Expected a conflicting channel unit to be rejected.");
    }
    catch (FormatException) { }
    Check(await Count("sensor_readings") == 12 && await Count("measurement_units") == 3,
        "conflicting measurement units are rejected without changing channel history");
    var replayIds = await writer.InsertBatchAsync(readings);
    Check(ids.SequenceEqual(replayIds) && await Count("sensor_readings") == 12, "replayed samples update existing rows");

    await db.ExecuteAsync("UPDATE sensor_devices SET device_name='Custom name',status='maintenance'; UPDATE sensor_channels SET channel_name='Custom channel',is_active=0;");
    var defaultUnit = await db.ExecuteScalarAsync<int>("SELECT default_unit_id FROM sensor_types LIMIT 1");
    await writer.InsertBatchAsync(readings.Reverse().ToArray());
    Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sensor_devices WHERE device_name='Custom name' AND status='maintenance'") == 2
        && await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sensor_channels WHERE channel_name='Custom channel' AND is_active=0") == 12
        && await db.ExecuteScalarAsync<int>("SELECT default_unit_id FROM sensor_types LIMIT 1") == defaultUnit,
        "ingestion preserves configured master data and type default unit");

    var valid = ShmsPayloadMapper.ToSensorReadings("test/vw03", "2026-10-15 10:01:00,123.4")[0];
    var invalid = new ShmsSensorReading
    {
        DeviceCode = "vw04", SensorCode = "VW", ChannelCode = "VW-FREQUENCY",
        MeasurementName = "frequency", UnitSymbol = "Hz", UnitCategory = "frequency",
        NumericValue = decimal.MaxValue, MeasuredAt = valid.MeasuredAt, QualityCode = "good"
    };
    await ExpectSqlFailure(() => writer.InsertBatchAsync([valid, invalid]));
    Check(await Count("sensor_readings") == 12 && await Count("sensor_devices") == 2
        && await Count("measurement_units") == 3 && await Count("sensor_types") == 1,
        "failure rolls back the entire payload including newly created master rows");
    await ExpectSqlFailure(() => db.ExecuteAsync("INSERT INTO sensor_readings(sensor_channel_id,measured_at,numeric_value) VALUES(-1,NOW(6),1)"));
    Console.WriteLine("PASS foreign key rejects orphaned readings");

    var logger = new LogWriterService(NullLogger<LogWriterService>.Instance);
    await logger.WriteRawAsync("test/tilt01", payload);
    await logger.WriteRawAsync("test/tilt02", payload);
    Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(DISTINCT sensor_device_id) FROM log_buffer") == 2,
        "raw logs reference the correct individual devices");
    Check(ShmsPayloadMapper.ResolveDeviceCode("test/tilt02", "{\"device_id\":\"custom-device\"}") == "custom-device"
        && ShmsPayloadMapper.ResolveDeviceCode("test/tilt02", "{broken") == "tilt02",
        "log identity respects JSON device IDs and tolerates malformed payloads");

    var channelId = await db.ExecuteScalarAsync<long>("SELECT MIN(id) FROM sensor_channels");
    var plan = await db.QuerySingleAsync("EXPLAIN SELECT numeric_value FROM sensor_readings WHERE sensor_channel_id=@channelId AND measured_at >= '2026-10-15' ORDER BY measured_at DESC LIMIT 10", new { channelId });
    Check((string?)plan.key == "uq_sensor_readings_channel_measured_at", "EXPLAIN uses channel/time index for sensor history");
    var deviceId = await db.ExecuteScalarAsync<long>("SELECT MIN(id) FROM sensor_devices");
    var logPlan = await db.QuerySingleAsync("EXPLAIN SELECT payload FROM log_buffer WHERE sensor_device_id=@deviceId ORDER BY time_stamp DESC LIMIT 1", new { deviceId });
    Check((string?)logPlan.key == "ix_log_buffer_sensor_device_time_stamp", "EXPLAIN uses device/time index for latest sensor log");
    Check(await CountIndexes() == indexes && await CountTables() == tables, "ingestion never creates additional tables or indexes");
    await RetentionChecks.RunAsync(db, writer, repository.FullName);
    await SystemSettingsChecks.RunAsync(db, repository.FullName);
    await LocalModeChecks.RunAsync(db);
    await LatestReadingsChecks.RunAsync(db, writer);
    Console.WriteLine("All MySQL integration checks passed.");

    Task<long> Count(string table) => db.ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM `{table}`");
    Task<long> CountIndexes() => db.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE()");
    Task<long> CountTables() => db.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE()");
}
finally
{
    Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", originalConnection);
    // The generated identifier contains only our fixed prefix and a Guid.
    await admin.ExecuteAsync($"DROP DATABASE `{database}`;");
}

static void Check(bool passed, string description)
{
    if (!passed) throw new InvalidOperationException($"FAIL {description}");
    Console.WriteLine($"PASS {description}");
}

static async Task ExpectSqlFailure(Func<Task> action)
{
    try { await action(); }
    catch (MySqlException) { return; }
    throw new InvalidOperationException("Expected MySQL to reject invalid data.");
}
