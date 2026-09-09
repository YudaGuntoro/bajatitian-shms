using Dapper;
using MySql.Data.MySqlClient;
using Worker.Shared;
using Worker.Configuration;

namespace Worker.Infrastructure.Persistence;

public sealed class SyncStatusWriterService : ISyncStatusWriterService
{
    private readonly ILogger<SyncStatusWriterService> _logger;

    public SyncStatusWriterService(ILogger<SyncStatusWriterService> logger)
    {
        _logger = logger;
    }

    public Task MarkSuccessAsync(string? endpointUrl, long redisBufferCount, CancellationToken cancellationToken = default) =>
        UpsertAsync(endpointUrl, true, null, redisBufferCount, 0, cancellationToken);

    public Task MarkFailureAsync(string? endpointUrl, string error, long redisBufferCount, CancellationToken cancellationToken = default) =>
        UpsertAsync(endpointUrl, false, error, redisBufferCount, 0, cancellationToken);

    public Task MarkDatabaseFallbackAsync(string? endpointUrl, long redisBufferCount, int movedCount, CancellationToken cancellationToken = default) =>
        UpsertAsync(endpointUrl, false, $"Redis buffer moved to local database after downtime threshold. Moved={movedCount}", redisBufferCount, movedCount, cancellationToken);

    private async Task UpsertAsync(
        string? endpointUrl,
        bool isOnline,
        string? error,
        long redisBufferCount,
        int movedCount,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(DatabaseConfig.MysqlConnString);
        await DbRetry.OpenWithRetryAsync(connection, _logger, "SyncStatus", cancellationToken);
        await EnsureTableAsync(connection, cancellationToken);

        const string sql = """
            INSERT INTO server_sync_status
                (id, server_name, endpoint_url, is_online, outage_started_at, last_success_at, last_failure_at, last_error, redis_buffer_count, db_spillover_count, updated_at)
            VALUES
                (1, 'Witon Server', @endpoint_url, @is_online,
                 IF(@is_online = 1, NULL, CURRENT_TIMESTAMP(6)),
                 IF(@is_online = 1, CURRENT_TIMESTAMP(6), NULL),
                 IF(@is_online = 1, NULL, CURRENT_TIMESTAMP(6)),
                 @last_error, @redis_buffer_count, @db_spillover_count, CURRENT_TIMESTAMP(6))
            ON DUPLICATE KEY UPDATE
                endpoint_url = VALUES(endpoint_url),
                is_online = VALUES(is_online),
                outage_started_at = CASE
                    WHEN VALUES(is_online) = 1 THEN NULL
                    WHEN server_sync_status.outage_started_at IS NULL THEN CURRENT_TIMESTAMP(6)
                    ELSE server_sync_status.outage_started_at
                END,
                last_success_at = IF(VALUES(is_online) = 1, CURRENT_TIMESTAMP(6), server_sync_status.last_success_at),
                last_failure_at = IF(VALUES(is_online) = 0, CURRENT_TIMESTAMP(6), server_sync_status.last_failure_at),
                last_error = VALUES(last_error),
                redis_buffer_count = VALUES(redis_buffer_count),
                db_spillover_count = server_sync_status.db_spillover_count + VALUES(db_spillover_count),
                updated_at = CURRENT_TIMESTAMP(6);
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            endpoint_url = string.IsNullOrWhiteSpace(endpointUrl) ? null : endpointUrl,
            is_online = isOnline,
            last_error = error,
            redis_buffer_count = redisBufferCount,
            db_spillover_count = movedCount
        }, cancellationToken: cancellationToken));
    }

    private static async Task EnsureTableAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = """
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
            ) ENGINE=InnoDB;
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }
}
