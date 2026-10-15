using Dapper;
using MySql.Data.MySqlClient;

namespace SHMS.Storage;

public sealed class DataRetentionStore(string connectionString)
{
    // Unknown upload state and alarm evidence must survive every cleanup policy.
    private const string EligibleReading = "r.uploaded_at IS NOT NULL AND NOT EXISTS (SELECT 1 FROM alert_events a WHERE a.sensor_reading_id = r.id)";
    private const int BatchSize = 1000;
    private const int MaxBatchesPerRun = 10;

    private async Task<MySqlConnection> OpenAsync(CancellationToken token)
    {
        var connection = new MySqlConnection(connectionString);
        try { await connection.OpenAsync(token); return connection; }
        catch { await connection.DisposeAsync(); throw; }
    }

    public async Task<RetentionSettings> GetSettingsAsync(CancellationToken token = default)
    {
        await using var db = await OpenAsync(token);
        return await ReadSettingsAsync(db, token);
    }

    private static Task<RetentionSettings> ReadSettingsAsync(MySqlConnection db, CancellationToken token) =>
        db.QuerySingleAsync<RetentionSettings>(new CommandDefinition("""
            SELECT enabled AS Enabled, reading_retention_days AS ReadingRetentionDays,
                   uploaded_log_retention_days AS UploadedLogRetentionDays,
                   cleanup_interval_minutes AS CleanupIntervalMinutes, max_reading_records AS MaxReadingRecords
            FROM data_retention_settings WHERE id = 1;
            """, cancellationToken: token));

    public async Task<RetentionSettings> SaveSettingsAsync(RetentionSettings settings, CancellationToken token = default)
    {
        settings.Validate();
        await using var db = await OpenAsync(token);
        await db.ExecuteAsync(new CommandDefinition("""
            UPDATE data_retention_settings SET enabled = @Enabled,
                reading_retention_days = @ReadingRetentionDays,
                uploaded_log_retention_days = @UploadedLogRetentionDays,
                cleanup_interval_minutes = @CleanupIntervalMinutes,
                max_reading_records = @MaxReadingRecords
            WHERE id = 1;
            """, settings, cancellationToken: token));
        return await ReadSettingsAsync(db, token);
    }

    public async Task<RetentionPreview> PreviewAsync(RetentionSettings settings, CancellationToken token = default)
    {
        settings.Validate();
        await using var db = await OpenAsync(token);
        var counts = await db.QuerySingleAsync<ReadingCounts>(new CommandDefinition($"""
            SELECT COUNT(*) AS Total,
                   COALESCE(SUM({EligibleReading}), 0) AS Eligible,
                   COALESCE(SUM(({EligibleReading}) AND r.ingested_at < DATE_SUB(CURRENT_TIMESTAMP(6), INTERVAL @ReadingRetentionDays DAY)), 0) AS Expired
            FROM sensor_readings r;
            """, settings, cancellationToken: token));
        var logs = await db.ExecuteScalarAsync<long>(new CommandDefinition("""
            SELECT COUNT(*) FROM log_buffer WHERE status = 'uploaded' AND uploaded_at IS NOT NULL
              AND uploaded_at < DATE_SUB(CURRENT_TIMESTAMP(6), INTERVAL @UploadedLogRetentionDays DAY);
            """, settings, cancellationToken: token));
        var excess = settings.MaxReadingRecords.HasValue ? Math.Max(0, counts.Total - settings.MaxReadingRecords.Value) : 0;
        return new RetentionPreview
        {
            ReadingsToDelete = settings.Enabled ? Math.Max(counts.Expired, Math.Min(excess, counts.Eligible)) : 0,
            LogsToDelete = settings.Enabled ? logs : 0,
            ProtectedReadings = counts.Total - counts.Eligible
        };
    }

    public async Task<RetentionStatus> GetStatusAsync(CancellationToken token = default)
    {
        await using var db = await OpenAsync(token);
        var status = await db.QuerySingleAsync<RetentionStatus>(new CommandDefinition("""
            SELECT last_cleanup_at AS LastCleanupAt, last_attempt_at AS LastAttemptAt,
                   deleted_readings AS DeletedReadings, deleted_logs AS DeletedLogs, last_error AS LastError,
                   work_pending AS CleanupPending,
                   (SELECT COUNT(*) FROM sensor_readings) AS TotalReadings,
                   (SELECT MIN(ingested_at) FROM sensor_readings) AS OldestReadingAt,
                   (SELECT COUNT(*) FROM log_buffer) AS TotalLogs,
                   (SELECT COUNT(*) FROM log_buffer WHERE status IN ('pending', 'failed')) AS PendingLogs
            FROM data_retention_status WHERE id = 1;
            """, cancellationToken: token));
        var settings = await ReadSettingsAsync(db, token);
        status.CleanupEnabled = settings.Enabled;
        status.NextCleanupAt = settings.Enabled && status.LastAttemptAt.HasValue
            ? status.LastAttemptAt.Value.AddSeconds(status.CleanupPending ? 30 : settings.CleanupIntervalMinutes * 60) : null;
        status.CapacityExceeded = settings.MaxReadingRecords.HasValue && status.TotalReadings > settings.MaxReadingRecords.Value;
        return status;
    }

    public async Task<bool> RunIfDueAsync(CancellationToken token = default)
    {
        await using var db = await OpenAsync(token);
        // A database-scoped advisory lock prevents two Worker instances cleaning concurrently.
        var lockName = "shms-retention-" + await db.ExecuteScalarAsync<string>(new CommandDefinition("SELECT LEFT(SHA2(DATABASE(),256),40)", cancellationToken: token));
        if (await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT GET_LOCK(@lockName,0)", new { lockName }, cancellationToken: token)) != 1)
            return false;
        long deletedReadings = 0, deletedLogs = 0;
        var attempted = false;
        var workPending = false;
        try
        {
            var settings = await ReadSettingsAsync(db, token);
            if (!settings.Enabled) return false;
            var due = await db.ExecuteScalarAsync<bool>(new CommandDefinition("""
                SELECT work_pending = 1 OR last_attempt_at IS NULL OR last_attempt_at <= DATE_SUB(CURRENT_TIMESTAMP(6), INTERVAL @CleanupIntervalMinutes MINUTE)
                FROM data_retention_status WHERE id = 1;
                """, settings, cancellationToken: token));
            if (!due) return false;
            await db.ExecuteAsync(new CommandDefinition("UPDATE data_retention_status SET last_attempt_at=CURRENT_TIMESTAMP(6), last_error=NULL WHERE id=1", cancellationToken: token));
            attempted = true;

            for (var batch = 0; batch < MaxBatchesPerRun; batch++)
            {
                token.ThrowIfCancellationRequested();
                // Re-read settings between batches, so disabling cleanup stops the next batch.
                settings = await ReadSettingsAsync(db, token);
                if (!settings.Enabled) break;
                long excess = 0;
                if (settings.MaxReadingRecords.HasValue)
                    excess = Math.Max(0, await db.ExecuteScalarAsync<long>(new CommandDefinition("SELECT COUNT(*) FROM sensor_readings", cancellationToken: token)) - settings.MaxReadingRecords.Value);
                var readings = await DeleteReadingBatchAsync(db, settings, excess, token);
                deletedReadings += readings;
                var logs = await db.ExecuteAsync(new CommandDefinition($"""
                    DELETE FROM log_buffer WHERE status='uploaded' AND uploaded_at IS NOT NULL
                      AND uploaded_at < DATE_SUB(CURRENT_TIMESTAMP(6), INTERVAL @UploadedLogRetentionDays DAY)
                    ORDER BY uploaded_at, id LIMIT {BatchSize};
                    """, settings, cancellationToken: token));
                deletedLogs += logs;
                workPending = readings > 0 || logs > 0;
                if (readings == 0 && logs == 0) break;
                await Task.Delay(50, token);
            }
            await WriteResultAsync(db, deletedReadings, deletedLogs, null, workPending);
            return true;
        }
        catch (Exception ex)
        {
            if (attempted) await WriteResultAsync(db, deletedReadings, deletedLogs, ex.Message);
            throw;
        }
        finally
        {
            await db.ExecuteScalarAsync<int?>(new CommandDefinition("SELECT RELEASE_LOCK(@lockName)", new { lockName }, cancellationToken: CancellationToken.None));
        }
    }

    private static async Task<int> DeleteReadingBatchAsync(MySqlConnection db, RetentionSettings settings, long excess, CancellationToken token)
    {
        // Expiry and capacity rules share one ordered scan. Capacity deletes at most the excess;
        // expired data may continue to be removed after that excess has been cleared.
        var capacity = excess > 0;
        var limit = capacity ? (int)Math.Min(BatchSize, excess) : BatchSize;
        await using var transaction = await db.BeginTransactionAsync(token);
        var ids = (await db.QueryAsync<long>(new CommandDefinition($"""
            SELECT r.id FROM sensor_readings r WHERE {EligibleReading}
              {(capacity ? "" : "AND r.ingested_at < DATE_SUB(CURRENT_TIMESTAMP(6), INTERVAL @ReadingRetentionDays DAY)")}
            ORDER BY r.ingested_at, r.id LIMIT {limit} FOR UPDATE;
            """, settings, transaction, cancellationToken: token))).ToArray();
        var deleted = ids.Length == 0 ? 0 : await db.ExecuteAsync(new CommandDefinition(
            "DELETE FROM sensor_readings WHERE id IN @ids", new { ids }, transaction, cancellationToken: token));
        await transaction.CommitAsync(token);
        return deleted;
    }

    private static Task WriteResultAsync(MySqlConnection db, long readings, long logs, string? error, bool workPending = false) =>
        db.ExecuteAsync(new CommandDefinition("""
            UPDATE data_retention_status SET
                last_cleanup_at = IF(@error IS NULL, CURRENT_TIMESTAMP(6), last_cleanup_at),
                deleted_readings=@readings, deleted_logs=@logs, last_error=@error, work_pending=@workPending WHERE id=1;
            """, new { readings, logs, error, workPending }, cancellationToken: CancellationToken.None));

    private sealed class ReadingCounts
    {
        public long Total { get; set; }
        public long Eligible { get; set; }
        public long Expired { get; set; }
    }
}
