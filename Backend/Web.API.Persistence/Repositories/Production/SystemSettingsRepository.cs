using Microsoft.EntityFrameworkCore;
using Web.API.Domain.Production;
using Web.API.Persistence.Context;

namespace Web.API.Persistence.Repositories.Production;

public sealed class SystemSettingsRepository : ISystemSettingsRepository
{
    private readonly AppDbContext _context;

    public SystemSettingsRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) =>
        _context.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS system_settings (
    id INT PRIMARY KEY,
    backup_db_location VARCHAR(500) NULL,
    backup_schedule VARCHAR(20) NOT NULL DEFAULT 'daily',
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6)
);
INSERT INTO system_settings (id, backup_schedule)
VALUES (1, 'daily')
ON DUPLICATE KEY UPDATE id = system_settings.id;", cancellationToken);

    public Task<SystemSetting?> GetAsync(CancellationToken cancellationToken = default) =>
        _context.SystemSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);

    public Task<SystemSetting?> GetForUpdateAsync(CancellationToken cancellationToken = default) =>
        _context.SystemSettings.FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);

    public void Add(SystemSetting settings) => _context.SystemSettings.Add(settings);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
