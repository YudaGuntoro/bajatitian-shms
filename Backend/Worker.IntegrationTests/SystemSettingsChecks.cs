using System.Text.Json;
using Dapper;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using Web.API.Domain.Production;
using Web.API.Persistence.Context;
using Web.API.Persistence.Repositories.Production;
using Web.API.Persistence.Services.Production;

internal static class SystemSettingsChecks
{
    public static async Task RunAsync(MySqlConnection db, string repository)
    {
        // Simulate an existing installation with arbitrary FK names and saved backup values.
        await db.ExecuteAsync("""
            INSERT INTO measurement_units(unit_category,unit_symbol,unit_name) VALUES
                ('pressure','MPa','Megapascal'),('cycle_time','s','Second');
            INSERT INTO system_settings(id,backup_db_location,backup_schedule,created_at,updated_at)
                VALUES(1,'existing-backup','weekly','2026-01-02 03:04:05','2026-01-03 04:05:06');
            ALTER TABLE system_settings ADD COLUMN pressure_unit_id INT NULL,
                ADD COLUMN cycle_time_unit_id INT NULL, ADD COLUMN plc_ip_address VARCHAR(80) NULL;
            UPDATE system_settings SET
                pressure_unit_id=(SELECT id FROM measurement_units WHERE unit_category='pressure' AND unit_symbol='MPa'),
                cycle_time_unit_id=(SELECT id FROM measurement_units WHERE unit_category='cycle_time' AND unit_symbol='s'),
                plc_ip_address='192.0.2.10', updated_at='2026-01-03 04:05:06';
            ALTER TABLE system_settings MODIFY pressure_unit_id INT NOT NULL, MODIFY cycle_time_unit_id INT NOT NULL,
                ADD CONSTRAINT legacy_pressure_reference FOREIGN KEY(pressure_unit_id) REFERENCES measurement_units(id),
                ADD CONSTRAINT legacy_cycle_reference FOREIGN KEY(cycle_time_unit_id) REFERENCES measurement_units(id);
            """);
        var unitsBefore = await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM measurement_units");
        var readingsBefore = await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sensor_readings");
        var migration = await File.ReadAllTextAsync(Path.Combine(repository,
            "Backend/Web.API.Persistence/Migrations/20261015_003_remove_legacy_system_settings.sql"));
        await db.ExecuteAsync(migration);
        await db.ExecuteAsync(migration);
        var columns = (await db.QueryAsync<string>("SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='system_settings' ORDER BY ORDINAL_POSITION")).ToArray();
        Check(columns.SequenceEqual(new[] { "id", "backup_db_location", "backup_schedule", "created_at", "updated_at" }),
            "legacy settings migration removes only obsolete columns and is repeatable");
        Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM system_settings WHERE backup_db_location='existing-backup' AND backup_schedule='weekly' AND created_at='2026-01-02 03:04:05' AND updated_at='2026-01-03 04:05:06'") == 1,
            "legacy cleanup preserves existing backup values and timestamps");
        Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM measurement_units") == unitsBefore
            && await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sensor_readings") == readingsBefore,
            "legacy cleanup preserves measurement master data and sensor history");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(Worker.Configuration.DatabaseConfig.MysqlConnString, new MySqlServerVersion(new Version(8, 0, 45))).Options;
        await using var context = new AppDbContext(options);
        var service = new SystemSettingsService(new SystemSettingsRepository(context));
        var response = await service.GetAsync();
        Check(response.BackupDbLocation == "existing-backup" && response.BackupSchedule == "weekly",
            "settings service reads the migrated schema");
        response = await service.UpdateAsync(new UpdateSystemSettingsRequest { BackupDbLocation = "updated-backup", BackupSchedule = "monthly" });
        var json = JsonSerializer.SerializeToElement(response);
        Check(response.BackupDbLocation == "updated-backup" && response.BackupSchedule == "monthly"
            && json.EnumerateObject().Count() == 2 && json.TryGetProperty("backup_schedule", out _),
            "settings update and response contain only current backup settings");
        Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='system_settings'") == 5
            && await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM measurement_units") == unitsBefore,
            "reading and saving settings never recreate production columns or unit seeds");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"FAIL {message}");
        Console.WriteLine($"PASS {message}");
    }
}
