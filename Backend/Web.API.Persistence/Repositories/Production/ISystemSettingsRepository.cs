using Web.API.Domain.Production;

namespace Web.API.Persistence.Repositories.Production;

public interface ISystemSettingsRepository
{
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);

    Task<SystemSetting?> GetAsync(CancellationToken cancellationToken = default);

    Task<SystemSetting?> GetForUpdateAsync(CancellationToken cancellationToken = default);

    void Add(SystemSetting settings);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
