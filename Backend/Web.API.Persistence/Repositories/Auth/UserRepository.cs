using Microsoft.EntityFrameworkCore;
using Web.API.Domain.Auth;
using Web.API.Persistence.Context;

namespace Web.API.Persistence.Repositories.Auth;

public sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<int> CountAsync(string? search, bool? isActive, CancellationToken cancellationToken = default) =>
        ApplyFilters(_context.Users.AsNoTracking(), search, isActive).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<AppUser>> GetPageAsync(
        int page,
        int limit,
        string? search,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        return await ApplyFilters(_context.Users.AsNoTracking().Include(x => x.Role), search, isActive)
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.FullName)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AppRole>> GetActiveRolesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Roles
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<AppRole?> GetActiveRoleAsync(int roleId, CancellationToken cancellationToken = default) =>
        _context.Roles.FirstOrDefaultAsync(x => x.Id == roleId && x.IsActive, cancellationToken);

    public Task<AppUser?> GetByIdWithRoleAsync(
        int id,
        bool asTracking = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Users.Include(x => x.Role).AsQueryable();
        if (!asTracking)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<AppUser?> GetByUsernameWithRoleAsync(
        string username,
        bool asTracking = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Users.Include(x => x.Role).AsQueryable();
        if (!asTracking)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync(x => x.Username == username, cancellationToken);
    }

    public Task<bool> UsernameExistsAsync(
        string username,
        int? excludedUserId = null,
        CancellationToken cancellationToken = default) =>
        _context.Users.AnyAsync(x =>
            x.Username == username &&
            (!excludedUserId.HasValue || x.Id != excludedUserId.Value),
            cancellationToken);

    public Task<bool> EmailExistsAsync(
        string email,
        int? excludedUserId = null,
        CancellationToken cancellationToken = default) =>
        _context.Users.AnyAsync(x =>
            x.Email == email &&
            (!excludedUserId.HasValue || x.Id != excludedUserId.Value),
            cancellationToken);

    public Task<int> CountOtherActiveAdminsAsync(int userId, CancellationToken cancellationToken = default) =>
        _context.Users
            .Include(x => x.Role)
            .CountAsync(x =>
                x.Id != userId &&
                x.IsActive &&
                x.Role != null &&
                x.Role.Name == AppUserRole.ADMIN.ToString(),
                cancellationToken);

    public void Add(AppUser user) => _context.Users.Add(user);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);

    private static IQueryable<AppUser> ApplyFilters(IQueryable<AppUser> query, string? search, bool? isActive)
    {
        if (isActive.HasValue)
        {
            query = query.Where(x => x.IsActive == isActive.Value);
        }

        var term = search?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(x =>
                x.Username.Contains(term) ||
                x.FullName.Contains(term) ||
                (x.Email != null && x.Email.Contains(term)) ||
                (x.Phone != null && x.Phone.Contains(term)));
        }

        return query;
    }
}
