using Newtonsoft.Json;
using Worker.Infrastructure.Persistence;

namespace Worker.Application.Handlers;

public sealed class LocalMqttMessageHandler(
    ILogger<LocalMqttMessageHandler> logger,
    ILogWriterService logWriter,
    IShmsSensorHandler sensorHandler) : IMqttMessageHandler
{
    public async Task WaitUntilReadyAsync(CancellationToken cancellationToken = default)
    {
        await logWriter.WaitUntilReadyAsync(cancellationToken);
        logger.LogInformation("[Worker] Local-only mode: MQTT messages go directly to MySQL. Redis and server uploads are disabled.");
    }

    public async Task HandleAsync(string topic, string payload, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // 'ignored' means intentionally excluded from forwarding, not successfully uploaded.
        // Preserve raw messages even when their sensor payload is malformed.
        await logWriter.WriteRawAsync(topic, payload, "ignored", cancellationToken: cancellationToken);
        if (!sensorHandler.CanHandle(topic)) return;
        try
        {
            await sensorHandler.InsertAsync(topic, payload, cancellationToken, uploaded: false);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            logger.LogWarning(ex, "[SHMS] Local MQTT payload was logged but could not be parsed. Topic={Topic}", topic);
        }
    }

    // No Redis connection, queue mutation, retry or HTTP request in local-only mode.
    public Task ReprocessBufferAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
