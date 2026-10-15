namespace Worker.Infrastructure.Mqtt;

public sealed record MqttBrokerOptions(
	string Host,
	int Port,
	string ClientId,
	string? Username,
	string? Password,
	bool UseTls)
{
	public string Signature =>
		$"{Host.Trim().ToLowerInvariant()}:{Port}:{ClientId.Trim()}:{Username?.Trim()}:{UseTls}";
}
