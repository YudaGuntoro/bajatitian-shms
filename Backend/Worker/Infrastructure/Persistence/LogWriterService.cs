using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using Worker.Shared;
using Worker.Configuration;
using Worker.Application.Mapping;

namespace Worker.Infrastructure.Persistence;

public sealed class LogWriterService : ILogWriterService
{
    private readonly ILogger<LogWriterService> _logger;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private volatile bool _ready;

    public LogWriterService(ILogger<LogWriterService> logger)
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
            await DbRetry.OpenWithRetryAsync(connection, _logger, "LogBuffer", cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, sensor_device_id, device_id, topic, payload, time_stamp,
                       status, retry_count, last_error, uploaded_at, created_at
                FROM log_buffer LIMIT 0;
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
            _ready = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async Task WriteRawAsync(
        string topic,
        string payload,
        string status = "pending",
        string? lastError = null,
        CancellationToken cancellationToken = default)
    {
        await WaitUntilReadyAsync(cancellationToken);
        await using var connection = new MySqlConnection(DatabaseConfig.MysqlConnString);
        await DbRetry.OpenWithRetryAsync(connection, _logger, "LogBuffer", cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO log_buffer
                (sensor_device_id, device_id, topic, payload, time_stamp, status, retry_count, last_error, uploaded_at, created_at)
            SELECT
                devices.id,
                @device_id,
                @topic,
                @payload,
                CURRENT_TIMESTAMP(6),
                @status,
                0,
                @last_error,
                IF(@status = 'uploaded', CURRENT_TIMESTAMP(6), NULL),
                CURRENT_TIMESTAMP(6)
            FROM (SELECT 1) seed
            LEFT JOIN sensor_devices devices ON devices.device_code = @device_id;
            """;
        command.Parameters.AddWithValue("@device_id", ShmsPayloadMapper.ResolveDeviceCode(topic, payload));
        command.Parameters.AddWithValue("@topic", topic);
        command.Parameters.AddWithValue("@payload", payload);
        command.Parameters.AddWithValue("@status", NormalizeStatus(status));
        command.Parameters.AddWithValue("@last_error", string.IsNullOrWhiteSpace(lastError) ? DBNull.Value : lastError);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string NormalizeStatus(string status) =>
        status.Trim().ToLowerInvariant() switch
        {
            "uploaded" => "uploaded",
            "failed" => "failed",
            "ignored" => "ignored",
            _ => "pending"
        };
}
