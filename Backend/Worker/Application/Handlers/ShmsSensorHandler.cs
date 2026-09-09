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
        !string.IsNullOrWhiteSpace(topic) && topic.Contains("shms", StringComparison.OrdinalIgnoreCase);

    public async Task InsertAsync(string topic, string payload, CancellationToken cancellationToken = default)
    {
        try
        {
            var reading = ShmsPayloadMapper.ToSensorReading(topic, payload);
            var insertedId = await _readingWriterService.InsertAsync(reading, cancellationToken);
            _logger.LogInformation(
                "[SHMS] Inserted reading Id={Id} Device={Device} Channel={Channel} Value={Value} Topic={Topic}",
                insertedId,
                reading.DeviceCode,
                reading.ChannelCode,
                reading.NumericValue,
                topic);
        }
        catch (JsonException ex)
        {
            throw new FormatException("MQTT payload must be valid JSON.", ex);
        }
    }
}
