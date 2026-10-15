using Microsoft.EntityFrameworkCore;
using Web.API.Domain.Production;
using Web.API.Persistence.Context;

namespace Web.API.Persistence.Repositories.Production;

public sealed class ShmsStatusRepository : IShmsStatusRepository
{
    private readonly AppDbContext _context;

    public ShmsStatusRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task EnsureServerSyncStatusTableAsync(CancellationToken cancellationToken = default) =>
        _context.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS server_sync_status (
    id INT PRIMARY KEY,
    server_name VARCHAR(100) NOT NULL,
    endpoint_url VARCHAR(500) NULL,
    is_online TINYINT(1) NOT NULL DEFAULT 0,
    outage_started_at DATETIME(6) NULL,
    last_success_at DATETIME(6) NULL,
    last_failure_at DATETIME(6) NULL,
    last_error TEXT NULL,
    redis_buffer_count BIGINT NOT NULL DEFAULT 0,
    db_spillover_count BIGINT NOT NULL DEFAULT 0,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6)
) ENGINE=InnoDB;", cancellationToken);

    public Task<DateTime?> GetLastMqttAtAsync(CancellationToken cancellationToken = default) =>
        _context.LogBuffers
            .AsNoTracking()
            .OrderByDescending(x => x.TimeStamp)
            .Select(x => (DateTime?)x.TimeStamp)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<ServerSyncStatus?> GetServerSyncStatusAsync(CancellationToken cancellationToken = default) =>
        _context.ServerSyncStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);
}
