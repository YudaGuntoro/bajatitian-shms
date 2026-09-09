using Worker.Domain.Models;

namespace Worker.Infrastructure.Http;

public interface IMainServerUploader
{
    bool IsConfigured { get; }

    string? UploadUrl { get; }

    Task<MainServerUploadResult> UploadAsync(string topic, string payload, CancellationToken cancellationToken = default);
}
