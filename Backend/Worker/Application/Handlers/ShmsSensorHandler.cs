using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Worker.Application.Mapping;
using Worker.Infrastructure.Persistence;
using Worker.Application.Handlers;

namespace Worker.Application.Handlers;

public sealed class ShmsSensorHandler : IShmsSensorHandler
{
    private readonly ILogger<ShmsSensorHandler> _logger;
    private readonly ISensorReadingWriterService _readingWriterService;

    public ShmsSensorHandler(
        ILogger<ShmsSensorHandler> logger,
        ISensorReadingWriterService readingWriterService)
    {
        _logger = logger;
        _readingWriterService = readingWriterService;
    }

    public bool CanHandle(string topic) =>
        !string.IsNullOrWhiteSpace(topic) &&
        (
            topic.Contains("shms", StringComparison.OrdinalIgnoreCase) ||
            topic.Contains("tilt", StringComparison.OrdinalIgnoreCase) ||
            topic.Contains("acc", StringComparison.OrdinalIgnoreCase) ||
            topic.Contains("vw", StringComparison.OrdinalIgnoreCase) ||
            topic.Contains("atrh", StringComparison.OrdinalIgnoreCase)
        );

    public async Task InsertAsync(string topic, string payload, CancellationToken cancellationToken = default, bool uploaded = false)
    {
        try
        {
            var readings = ShmsPayloadMapper.ToSensorReadings(topic, payload);
            var insertedIds = await _readingWriterService.InsertBatchAsync(readings, cancellationToken, uploaded);
            for (var index = 0; index < readings.Count; index++)
            {
                var reading = readings[index];
                _logger.LogInformation(
                    "[SHMS] Inserted reading Id={Id} Device={Device} Channel={Channel} Value={Value} Topic={Topic}",
                    insertedIds[index],
                    reading.DeviceCode,
                    reading.ChannelCode,
                    reading.NumericValue,
                    topic);
            }
        }
        catch (JsonException ex)
        {
            throw new FormatException("MQTT payload must be valid JSON.", ex);
        }
    }
}
