namespace Worker.Domain.Models;

public sealed class BufferedMqttMessage
{
    public DateTime BufferedAt { get; init; }
    public string Topic { get; init; } = string.Empty;
    public string Payload { get; init; } = string.Empty;
    public bool RawLogged { get; init; }
    public string? Error { get; init; }
}
