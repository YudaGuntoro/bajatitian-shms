using System.Threading;
using System.Threading.Tasks;

namespace Worker.Infrastructure.Persistence;

public interface ILogWriterService
{
	Task WaitUntilReadyAsync(CancellationToken cancellationToken = default(CancellationToken));

	Task WriteRawAsync(
		string topic,
		string payload,
		string status = "pending",
		string? lastError = null,
		CancellationToken cancellationToken = default(CancellationToken));
}
