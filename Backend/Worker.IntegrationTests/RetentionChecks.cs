using System.ComponentModel.DataAnnotations;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using MySql.Data.MySqlClient;
using SHMS.Storage;
using Worker.Application.Mapping;
using Worker.Configuration;
using Worker.Domain.Models;
using Worker.Infrastructure.Persistence;

internal static class RetentionChecks
{
    public static async Task RunAsync(MySqlConnection db, SensorReadingWriterService writer, string repository)
    {
        var migration = await File.ReadAllTextAsync(Path.Combine(repository, "Backend/Web.API.Persistence/Migrations/20261015_002_data_retention.sql"));
        await db.ExecuteAsync("ALTER TABLE sensor_readings DROP INDEX ix_sensor_readings_retention; ALTER TABLE sensor_readings DROP COLUMN uploaded_at; ALTER TABLE log_buffer DROP INDEX ix_log_buffer_uploaded_retention;");
        await db.ExecuteAsync(migration);
        var indexCount = await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE()");
        await db.ExecuteAsync(migration);
        Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE()") == indexCount,
            "retention migration upgrades legacy readings and remains repeatable");
        var store = new DataRetentionStore(DatabaseConfig.MysqlConnString);
        Check(!(await store.GetSettingsAsync()).Enabled && !await store.RunIfDueAsync(), "cleanup starts disabled and performs no deletion");
        var rejected = false;
        try { await store.SaveSettingsAsync(new RetentionSettings { ReadingRetentionDays = 0 }); }
        catch (ValidationException) { rejected = true; }
        Check(rejected && (await store.GetSettingsAsync()).ReadingRetentionDays == 30, "invalid policies cannot be persisted");

        var expired = await Insert("vw10", true);
        var pending = await Insert("vw11", false);
        var fresh = await Insert("vw12", true);
        var backfill = await Insert("vw13", true, "1990-01-01 00:00:00");
        var alarm = await Insert("vw14", true);
        await db.ExecuteAsync("UPDATE sensor_readings SET ingested_at=DATE_SUB(CURRENT_TIMESTAMP(6),INTERVAL 40 DAY) WHERE id IN @ids", new { ids = new[] { expired, pending, alarm } });
        await db.ExecuteAsync("""
            INSERT INTO alert_rules(sensor_channel_id,rule_code,rule_name,operator,threshold_value)
            SELECT sensor_channel_id,'RETENTION-TEST','Retention test','>',1 FROM sensor_readings WHERE id=@alarm;
            INSERT INTO alert_events(alert_rule_id,sensor_reading_id,triggered_at,severity,message)
            VALUES(LAST_INSERT_ID(),@alarm,CURRENT_TIMESTAMP(6),'warning','Alarm evidence');
            """, new { alarm });
        var oldIngestedAt = await db.ExecuteScalarAsync<DateTime>("SELECT ingested_at FROM sensor_readings WHERE id=@expired", new { expired });
        await Insert("vw10", true);
        Check(await db.ExecuteScalarAsync<DateTime>("SELECT ingested_at FROM sensor_readings WHERE id=@expired", new { expired }) == oldIngestedAt,
            "replay preserves first ingestion time for FIFO");
        var logWriter = new LogWriterService(NullLogger<LogWriterService>.Instance);
        await logWriter.WriteRawAsync("test/vw10", "old-uploaded", "uploaded");
        await logWriter.WriteRawAsync("test/vw11", "old-failed", "failed");
        await logWriter.WriteRawAsync("test/vw11", "old-pending", "pending");
        await logWriter.WriteRawAsync("test/vw12", "recent-upload", "uploaded");
        await db.ExecuteAsync("UPDATE log_buffer SET time_stamp=DATE_SUB(NOW(6),INTERVAL 40 DAY) WHERE payload IN ('old-uploaded','old-failed','old-pending','recent-upload'); UPDATE log_buffer SET uploaded_at=DATE_SUB(NOW(6),INTERVAL 10 DAY) WHERE payload='old-uploaded';");
        var policy = new RetentionSettings { Enabled = true, ReadingRetentionDays = 30, UploadedLogRetentionDays = 7, CleanupIntervalMinutes = 60 };
        var preview = await store.PreviewAsync(policy);
        Check(preview.ReadingsToDelete == 1 && preview.LogsToDelete == 1 && preview.ProtectedReadings > 0,
            "preview excludes unsent data, unknown legacy delivery, fresh ingestion and alarm evidence");
        await store.SaveSettingsAsync(policy);
        Check(await store.RunIfDueAsync() && !await store.RunIfDueAsync(), "cleanup follows the database-configured interval");
        Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sensor_readings WHERE id=@expired", new { expired }) == 0
            && await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sensor_readings WHERE id IN @ids", new { ids = new[] { pending, fresh, backfill, alarm } }) == 4,
            "expiry deletes only confirmed expired readings");
        Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM log_buffer WHERE payload='old-uploaded'") == 0
            && await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM log_buffer WHERE payload IN ('old-failed','old-pending','recent-upload')") == 3,
            "log expiry protects pending/failed messages and uses upload time");
        var status = await store.GetStatusAsync();
        Check(status.DeletedReadings == 1 && status.DeletedLogs == 1 && status.LastCleanupAt.HasValue && status.NextCleanupAt.HasValue && status.LastError is null,
            "cleanup publishes counts and the next schedule");

        // Seed enough eligible samples to exercise the optional count cap and FIFO order.
        var capacityReadings = Enumerable.Range(0, 105).Select(i => new ShmsSensorReading
        {
            DeviceCode = "vw99", SensorCode = "VW", ChannelCode = "VW-FREQUENCY", MeasurementName = "frequency",
            UnitSymbol = "Hz", UnitCategory = "frequency", NumericValue = i,
            MeasuredAt = new DateTime(2026, 1, 1).AddSeconds(i), QualityCode = "good"
        }).ToArray();
        var capacityIds = await writer.InsertBatchAsync(capacityReadings, uploaded: true);
        await db.ExecuteAsync("UPDATE sensor_readings SET ingested_at=DATE_ADD(DATE_SUB(NOW(6),INTERVAL 2 DAY),INTERVAL id SECOND) WHERE id IN @ids", new { ids = capacityIds });
        policy.MaxReadingRecords = 100;
        await store.SaveSettingsAsync(policy);
        var beforeCapacity = await db.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM sensor_readings");
        var expected = beforeCapacity - 100;
        Check((await store.PreviewAsync(policy)).ReadingsToDelete == expected, "capacity preview counts overflow without double-counting expiry");
        await MakeDue();
        await store.RunIfDueAsync();
        Check(await db.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM sensor_readings") == 100
            && await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sensor_readings WHERE id IN @ids", new { ids = capacityIds.Take((int)expected).ToArray() }) == 0
            && await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sensor_readings WHERE id=@id", new { id = capacityIds.Last() }) == 1,
            "capacity cleanup removes the oldest eligible rows first");

        // Replacing an already delivered sample with unconfirmed data revokes its old acknowledgement.
        await writer.InsertBatchAsync([new ShmsSensorReading
        {
            DeviceCode = "vw99", SensorCode = "VW", ChannelCode = "VW-FREQUENCY", MeasurementName = "frequency",
            UnitSymbol = "Hz", UnitCategory = "frequency", NumericValue = -1,
            MeasuredAt = capacityReadings[^1].MeasuredAt, QualityCode = "good"
        }]);
        Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sensor_readings WHERE id=@id AND uploaded_at IS NULL", new { id = capacityIds.Last() }) == 1,
            "unconfirmed corrections cannot reuse a previous upload acknowledgement");

        await db.ExecuteAsync("UPDATE sensor_readings SET uploaded_at=NULL;");
        foreach (var number in Enumerable.Range(20, 10)) await Insert($"vw{number}", false);
        await MakeDue();
        await store.RunIfDueAsync();
        Check((await store.GetStatusAsync()).CapacityExceeded && (await store.PreviewAsync(policy)).ReadingsToDelete == 0,
            "capacity never overrides protection for unsent readings");

        await using var competing = new MySqlConnection(DatabaseConfig.MysqlConnString);
        await competing.OpenAsync();
        var lockName = await competing.ExecuteScalarAsync<string>("SELECT CONCAT('shms-retention-',LEFT(SHA2(DATABASE(),256),40))");
        await competing.ExecuteScalarAsync<int>("SELECT GET_LOCK(@lockName,0)", new { lockName });
        Check(!await store.RunIfDueAsync(), "only one Worker instance can clean a database at a time");
        await competing.ExecuteScalarAsync<int>("SELECT RELEASE_LOCK(@lockName)", new { lockName });
        policy.Enabled = false;
        await store.SaveSettingsAsync(policy);
        Check(!await store.RunIfDueAsync(), "disabling the persisted policy stops cleanup");

        // A large backlog must resume on the next Worker tick, not wait another full interval.
        await db.ExecuteAsync("""
            INSERT INTO sensor_readings(sensor_channel_id,measured_at,numeric_value,ingested_at,uploaded_at)
            WITH digits AS (SELECT 0 n UNION ALL SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4
                UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7 UNION ALL SELECT 8 UNION ALL SELECT 9),
            numbers AS (SELECT a.n+10*b.n+100*c.n+1000*d.n+10000*e.n AS n
                FROM digits a CROSS JOIN digits b CROSS JOIN digits c CROSS JOIN digits d CROSS JOIN digits e)
            SELECT (SELECT MIN(id) FROM sensor_channels), DATE_ADD('2040-01-01',INTERVAL n SECOND), n,
                DATE_SUB(NOW(6),INTERVAL 40 DAY), DATE_SUB(NOW(6),INTERVAL 40 DAY)
            FROM numbers WHERE n < 11005;
            """);
        policy.Enabled = true;
        await store.SaveSettingsAsync(policy);
        await MakeDue();
        Check((await store.PreviewAsync(policy)).ReadingsToDelete == 11005,
            "overlapping expiry and capacity limits count each eligible reading once");
        await store.RunIfDueAsync();
        Check((await store.GetStatusAsync()).DeletedReadings == 10000 && (await store.GetStatusAsync()).CleanupPending,
            "each cleanup pass has a bounded transaction workload");
        Check(await store.RunIfDueAsync() && (await store.GetStatusAsync()).DeletedReadings == 1005
            && !(await store.GetStatusAsync()).CleanupPending,
            "backlog continues before the next interval while protected data survives");
        policy.Enabled = false;
        await store.SaveSettingsAsync(policy);

        var plan = await db.QuerySingleAsync("EXPLAIN SELECT id FROM sensor_readings WHERE ingested_at < NOW() AND uploaded_at IS NOT NULL ORDER BY ingested_at,id LIMIT 1000");
        Check(((string?)plan.possible_keys)?.Contains("ix_sensor_readings_retention") == true, "retention index supports the FIFO scan");

        async Task<long> Insert(string device, bool uploaded, string timestamp = "2026-10-15 10:00:00") =>
            (await writer.InsertBatchAsync(ShmsPayloadMapper.ToSensorReadings($"test/{device}", $"{timestamp},100.5"), uploaded: uploaded))[0];
        Task MakeDue() => db.ExecuteAsync("UPDATE data_retention_status SET last_attempt_at=NULL WHERE id=1");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"FAIL {message}");
        Console.WriteLine($"PASS {message}");
    }
}
