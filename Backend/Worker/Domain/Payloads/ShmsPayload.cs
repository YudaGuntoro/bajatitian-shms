using Newtonsoft.Json.Linq;

namespace Worker.Domain.Payloads;

public sealed class ShmsPayload
{
    public string Topic { get; init; } = string.Empty;
    public JObject Data { get; init; } = new();
}
