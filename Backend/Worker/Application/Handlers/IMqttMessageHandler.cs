namespace Worker.Application.Handlers;

public interface IMqttMessageHandler
{
    Task WaitUntilReadyAsync(CancellationToken cancellationToken = default);

    Task HandleAsync(string topic, string payload, CancellationToken cancellationToken = default);

    Task ReprocessBufferAsync(CancellationToken cancellationToken = default);
}
