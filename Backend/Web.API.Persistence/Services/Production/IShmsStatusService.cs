using Web.API.Domain.Production;

namespace Web.API.Persistence.Services.Production;

public interface IShmsStatusService
{
    Task<ShmsSystemStatusResponse> GetAsync(CancellationToken cancellationToken = default);
}
