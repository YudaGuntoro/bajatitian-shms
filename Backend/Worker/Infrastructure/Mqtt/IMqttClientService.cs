using System.Threading;
using System.Threading.Tasks;

namespace Worker.Infrastructure.Mqtt;

public interface IMqttClientService : IMqttPublisher
{
	void Configure(string brokerHost, int brokerPort);

	Task ConnectAsync(CancellationToken cancellationToken = default(CancellationToken));

	Task SubscribeAsync(string topic, CancellationToken cancellationToken = default(CancellationToken));
}
