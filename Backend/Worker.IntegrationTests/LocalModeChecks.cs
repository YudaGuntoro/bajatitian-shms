using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Worker.Application.Handlers;
using Worker.Configuration;
using Worker.Infrastructure.Http;
using Worker.Infrastructure.Persistence;
using Worker.Infrastructure.Redis;
using SHMS.Storage;

internal static class LocalModeChecks
{
    public static async Task RunAsync(MySql.Data.MySqlClient.MySqlConnection db)
    {
        Check(new WorkerRuntimeOptions().LocalOnly, "local-only delivery is the default");
        var original = Environment.GetEnvironmentVariable("WORKER_LOCAL_ONLY");
        try
        {
            Environment.SetEnvironmentVariable("WORKER_LOCAL_ONLY", "false");
            Check(!WorkerRuntimeOptions.Load().LocalOnly, "forwarding can be restored explicitly through configuration");
            Environment.SetEnvironmentVariable("WORKER_LOCAL_ONLY", "true");
            Check(WorkerRuntimeOptions.Load().LocalOnly, "environment configuration enables local-only mode");
            Environment.SetEnvironmentVariable("WORKER_LOCAL_ONLY", "invalid");
            var rejected = false;
            try { WorkerRuntimeOptions.Load(); } catch (FormatException) { rejected = true; }
            Check(rejected, "invalid delivery configuration fails without enabling network delivery");
        }
        finally { Environment.SetEnvironmentVariable("WORKER_LOCAL_ONLY", original); }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWorkerDelivery(new WorkerRuntimeOptions(LocalOnly: true));
        await using var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<IMqttMessageHandler>();
        Check(handler is LocalMqttMessageHandler && provider.GetService<IMainServerUploader>() is null
            && provider.GetService<IRedisMqttMessageBuffer>() is null && provider.GetService<ISyncStatusWriterService>() is null,
            "local Worker registers no Redis, HTTP uploader or remote sync services");
        await handler.WaitUntilReadyAsync();
        const string topic = "testing/tilt88";
        const string payload = "2026-10-15 11:00:00,1.1,2.2,20,21,100,200";
        await handler.HandleAsync(topic, payload);
        Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM log_buffer WHERE topic=@topic AND payload=@payload AND status='ignored' AND uploaded_at IS NULL AND last_error IS NULL", new { topic, payload }) == 1,
            "local messages are logged without upload failures or false success");
        Check(await db.ExecuteScalarAsync<int>("""
            SELECT COUNT(*) FROM sensor_readings r
            JOIN sensor_channels c ON c.id=r.sensor_channel_id JOIN sensor_devices d ON d.id=c.sensor_device_id
            WHERE d.device_code='tilt88' AND r.uploaded_at IS NULL;
            """) == 6, "local MQTT data is normalized into MySQL immediately without Redis or a main server");
        var beforeRetry = await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sensor_readings");
        await handler.HandleAsync(topic, payload);
        await handler.ReprocessBufferAsync();
        Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sensor_readings") == beforeRetry,
            "local mode keeps retry deduplication and makes queue replay a no-op");
        await handler.HandleAsync(topic, "{malformed");
        Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM log_buffer WHERE topic=@topic AND payload='{malformed' AND status='ignored'", new { topic }) == 1
            && await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sensor_readings") == beforeRetry,
            "malformed local payloads remain in the raw log without invalid readings");
        var preview = await new DataRetentionStore(DatabaseConfig.MysqlConnString).PreviewAsync(new RetentionSettings { Enabled = true, MaxReadingRecords = 100 });
        Check(preview.ReadingsToDelete == 0, "local data is not treated as confirmed uploaded data by FIFO cleanup");

        // Resolving the remote handler verifies the original mode remains available; no methods are invoked.
        var remoteServices = new ServiceCollection();
        remoteServices.AddLogging();
        remoteServices.AddWorkerDelivery(new WorkerRuntimeOptions(LocalOnly: false));
        await using var remoteProvider = remoteServices.BuildServiceProvider();
        Check(remoteProvider.GetRequiredService<IMqttMessageHandler>() is MqttMessageRouter,
            "remote delivery implementation remains available when explicitly enabled");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"FAIL {message}");
        Console.WriteLine($"PASS {message}");
    }
}
