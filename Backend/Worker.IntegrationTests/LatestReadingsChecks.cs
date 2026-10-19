using Dapper;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using Web.API.Persistence.Context;
using Web.API.Persistence.Repositories.Production;
using Web.API.Persistence.Services.Production;
using Worker.Application.Mapping;
using Worker.Configuration;
using Worker.Infrastructure.Persistence;

internal static class LatestReadingsChecks
{
    public static async Task RunAsync(MySqlConnection db, SensorReadingWriterService writer)
    {
        await writer.InsertBatchAsync(ShmsPayloadMapper.ToSensorReadings("adit/rnd/test123/vw09", "2026-10-16 10:00:00,1500.5"));
        await writer.InsertBatchAsync(ShmsPayloadMapper.ToSensorReadings("adit/rnd/test123/vw09", "2026-10-16 10:05:00,2000.125"));
        // Receiving an older historical sample later must not replace the newest measurement.
        await writer.InsertBatchAsync(ShmsPayloadMapper.ToSensorReadings("adit/rnd/test123/vw09", "2026-10-16 09:00:00,900"));
        await writer.InsertBatchAsync(ShmsPayloadMapper.ToSensorReadings("adit/rnd/test123/tilt09", "2026-10-16 10:00:00,0,2.5,20,21,100,200"));
        await db.ExecuteAsync("""
            UPDATE sensor_readings r JOIN sensor_channels c ON c.id=r.sensor_channel_id
            JOIN sensor_devices d ON d.id=c.sensor_device_id
            SET r.ingested_at=DATE_SUB(NOW(6),INTERVAL 2 HOUR) WHERE d.device_code IN ('vw09','tilt09');
            INSERT INTO log_buffer(device_id,topic,payload,status,time_stamp)
            VALUES('vw09','adit/rnd/test123/vw09','2026-10-16 09:00:00,900','ignored',DATE_SUB(NOW(6),INTERVAL 2 HOUR));
            INSERT INTO log_buffer(device_id,topic,payload,status)
            WITH RECURSIVE numbers AS (SELECT 1 n UNION ALL SELECT n+1 FROM numbers WHERE n<60)
            SELECT 'other-device','testing/other','newer activity','ignored' FROM numbers;
            """);
        Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM (SELECT device_id FROM log_buffer ORDER BY time_stamp DESC,id DESC LIMIT 50) latest WHERE device_id='vw09'") == 0,
            "fixture keeps sensor data outside the 50 latest activity records");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(DatabaseConfig.MysqlConnString, new MySqlServerVersion(new Version(8, 0, 45))).Options;
        await using (var context = new AppDbContext(options))
        {
            var service = new ShmsStatusService(new ShmsStatusRepository(context));
            var readings = await service.GetLatestReadingsAsync();
            var vw = readings.Single(r => r.DeviceCode == "vw09");
            Check(vw.Value == 2000.125m && vw.Unit == "Hz" && vw.MeasuredAt == new DateTime(2026, 10, 16, 10, 5, 0),
                "latest readings select measurement time and retain accurate values and units");
            Check(vw.Topic == "adit/rnd/test123/vw09" && vw.ReceivedAgeSeconds >= 3600
                && vw.ReceivedAt < (await db.ExecuteScalarAsync<DateTime>("SELECT NOW(6)")).AddHours(-1),
                "offline sensor data remains available independently of recent activity pagination");
            var tilt = readings.Where(r => r.DeviceCode == "tilt09").ToArray();
            Check(tilt.Length == 6 && tilt.Any(r => r.MeasurementName == "tilt" && r.Axis == "X" && r.Value == 0 && r.Unit == "deg")
                && tilt.Any(r => r.MeasurementName == "temperature" && r.Axis == "X" && r.Value == 20 && r.Unit == "C"),
                "each sensor channel retains its own last value, including zero and differing units");
        }
        await db.ExecuteAsync("DELETE FROM log_buffer WHERE device_id='vw09';");
        await using (var context = new AppDbContext(options))
        {
            var repository = new ShmsStatusRepository(context);
            var vw = (await repository.GetLatestReadingsAsync()).Single(r => r.DeviceCode == "vw09");
            Check(vw.Value == 2000.125m && vw.Topic is null && vw.ReceivedAt < (await db.ExecuteScalarAsync<DateTime>("SELECT NOW(6)")).AddHours(-1),
                "last values survive a fresh application context and raw log removal");
        }
        var plans = (await db.QueryAsync("EXPLAIN " + ShmsStatusRepository.LatestReadingsSql)).ToArray();
        var indexed = plans.Any(row => (string?)row.table == "latest" && ((string?)row.possible_keys)?.Contains("uq_sensor_readings_channel_measured_at") == true)
            && plans.Any(row => (string?)row.table == "latest_log" && ((string?)row.possible_keys)?.Contains("ix_log_buffer_device_time_stamp") == true);
        if (!indexed)
            foreach (var row in plans) Console.WriteLine($"Plan: table={row.table}, key={row.key}, possible={row.possible_keys}");
        Check(indexed, "latest-value lookup supports existing channel/time and device/time indexes");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"FAIL {message}");
        Console.WriteLine($"PASS {message}");
    }
}
