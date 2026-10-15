using Web.API.Domain.Auth;

namespace Web.API.Persistence.Repositories.Auth;

public interface IUserRepository
{
    Task<int> CountAsync(string? search, bool? isActive, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppUser>> GetPageAsync(
        int page,
        int limit,
        string? search,
        bool? isActive,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppRole>> GetActiveRolesAsync(CancellationToken cancellationToken = default);

    Task<AppRole?> GetActiveRoleAsync(int roleId, CancellationToken cancellationToken = default);

    Task<AppUser?> GetByIdWithRoleAsync(
        int id,
        bool asTracking = false,
        CancellationToken cancellationToken = default);

    Task<AppUser?> GetByUsernameWithRoleAsync(
        string username,
        bool asTracking = false,
        CancellationToken cancellationToken = default);

    Task<bool> UsernameExistsAsync(
        string username,
        int? excludedUserId = null,
        CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(
        string email,
        int? excludedUserId = null,
        CancellationToken cancellationToken = default);

    Task<int> CountOtherActiveAdminsAsync(int userId, CancellationToken cancellationToken = default);

    void Add(AppUser user);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
