using Web.API.Domain.Production;
using Web.API.Persistence.Repositories.Production;

namespace Web.API.Persistence.Services.Production;

public sealed class SystemSettingsService : ISystemSettingsService
{
    private readonly ISystemSettingsRepository _repository;

    public SystemSettingsService(ISystemSettingsRepository repository)
    {
        _repository = repository;
    }

    public async Task<SystemSettingsResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        await _repository.EnsureSchemaAsync(cancellationToken);
        return ToResponse(await _repository.GetAsync(cancellationToken));
    }

    public async Task<SystemSettingsResponse> UpdateAsync(
        UpdateSystemSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        await _repository.EnsureSchemaAsync(cancellationToken);
        var settings = await _repository.GetForUpdateAsync(cancellationToken);
        if (settings is null)
        {
            settings = new SystemSetting { Id = 1, CreatedAt = DateTime.Now };
            _repository.Add(settings);
        }

        settings.BackupDbLocation = request.BackupDbLocation;
        settings.BackupSchedule = string.IsNullOrWhiteSpace(request.BackupSchedule)
            ? "daily"
            : request.BackupSchedule.Trim();
        settings.UpdatedAt = DateTime.Now;

        await _repository.SaveChangesAsync(cancellationToken);
        return ToResponse(await _repository.GetAsync(cancellationToken));
    }

    private static SystemSettingsResponse ToResponse(SystemSetting? settings) =>
        new()
        {
            BackupDbLocation = settings?.BackupDbLocation ?? string.Empty,
            BackupSchedule = settings?.BackupSchedule ?? "daily"
        };
}
