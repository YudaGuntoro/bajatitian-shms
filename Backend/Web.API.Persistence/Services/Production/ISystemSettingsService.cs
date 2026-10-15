using Web.API.Domain.Production;

namespace Web.API.Persistence.Services.Production;

public interface ISystemSettingsService
{
    Task<SystemSettingsResponse> GetAsync(CancellationToken cancellationToken = default);

    Task<SystemSettingsResponse> UpdateAsync(
        UpdateSystemSettingsRequest request,
        CancellationToken cancellationToken = default);
}
