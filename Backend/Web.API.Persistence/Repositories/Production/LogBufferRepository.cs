using Microsoft.EntityFrameworkCore;
using Web.API.Domain.Production;
using Web.API.Persistence.Context;

namespace Web.API.Persistence.Repositories.Production;

public sealed class LogBufferRepository : ILogBufferRepository
{
    private readonly AppDbContext _context;

    public LogBufferRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        _context.LogBuffers
            .AsNoTracking()
            .CountAsync(cancellationToken);

    public async Task<IReadOnlyList<LogBuffer>> GetPageAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return await _context.LogBuffers
            .AsNoTracking()
            .OrderByDescending(x => x.TimeStamp)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }
}
