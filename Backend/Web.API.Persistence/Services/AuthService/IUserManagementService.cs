using Web.API.Domain.Auth;

namespace Web.API.Persistence.Services.AuthService;

public interface IUserManagementService
{
    Task<UserPageResult> GetUsersAsync(
        int page,
        int limit,
        string? search,
        bool? isActive,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppRole>> GetRolesAsync(CancellationToken cancellationToken = default);

    Task<UserResponse?> GetUserAsync(int id, CancellationToken cancellationToken = default);

    Task<UserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);

    Task<UserResponse?> UpdateUserAsync(
        int id,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default);

    Task<UserResponse?> DeactivateUserAsync(int id, CancellationToken cancellationToken = default);
}
