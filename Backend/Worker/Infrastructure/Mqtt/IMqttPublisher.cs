using System.Threading;
using System.Threading.Tasks;

namespace Worker.Infrastructure.Mqtt;

public interface IMqttPublisher
{
	Task PublishAsync(string topic, string payload, bool retain = false, int qos = 0, CancellationToken cancellationToken = default(CancellationToken));
}
