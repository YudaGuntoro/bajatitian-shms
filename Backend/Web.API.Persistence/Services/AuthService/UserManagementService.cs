using Web.API.Domain.Auth;
using Web.API.Persistence.Repositories.Auth;
using Web.API.Persistence.Services.Shared;

namespace Web.API.Persistence.Services.AuthService;

public sealed class UserManagementService : IUserManagementService
{
    private const int DefaultPage = 1;
    private const int DefaultLimit = 10;
    private const int MaxLimit = 100;

    private readonly IUserRepository _users;

    public UserManagementService(IUserRepository users)
    {
        _users = users;
    }

    public async Task<UserPageResult> GetUsersAsync(
        int page,
        int limit,
        string? search,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(DefaultPage, page);
        limit = Math.Clamp(limit, 1, MaxLimit);

        var total = await _users.CountAsync(search, isActive, cancellationToken);
        var users = await _users.GetPageAsync(page, limit, search, isActive, cancellationToken);

        return new UserPageResult
        {
            Users = users.Select(ToResponse).ToList(),
            Page = page,
            Limit = limit,
            Total = total
        };
    }

    public Task<IReadOnlyList<AppRole>> GetRolesAsync(CancellationToken cancellationToken = default) =>
        _users.GetActiveRolesAsync(cancellationToken);

    public async Task<UserResponse?> GetUserAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdWithRoleAsync(id, cancellationToken: cancellationToken);
        return user is null ? null : ToResponse(user);
    }

    public async Task<UserResponse> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var username = NormalizeRequired(request.Username, "Username is required.");
        var fullName = NormalizeRequired(request.FullName, "Full name is required.");
        var password = NormalizeRequired(request.Password, "Password is required.");
        var email = NormalizeOptional(request.Email);
        var phone = NormalizeOptional(request.Phone);
        var role = await GetActiveRoleAsync(request.RolesId, cancellationToken);

        await EnsureUsernameAvailableAsync(username, null, cancellationToken);
        await EnsureEmailAvailableAsync(email, null, cancellationToken);

        var salt = AuthPasswordHasher.CreateSalt();
        var now = DateTime.Now;
        var user = new AppUser
        {
            Username = username,
            FullName = fullName,
            Email = email,
            Phone = phone,
            RolesId = role.Id,
            Role = role,
            IsActive = request.IsActive,
            PasswordSalt = salt,
            PasswordHash = AuthPasswordHasher.HashPassword(password, salt),
            CreatedAt = now,
            UpdatedAt = now
        };

        _users.Add(user);
        await _users.SaveChangesAsync(cancellationToken);

        return ToResponse(user);
    }

    public async Task<UserResponse?> UpdateUserAsync(
        int id,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdWithRoleAsync(id, asTracking: true, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var username = NormalizeRequired(request.Username, "Username is required.");
        var fullName = NormalizeRequired(request.FullName, "Full name is required.");
        var email = NormalizeOptional(request.Email);
        var phone = NormalizeOptional(request.Phone);
        var role = await GetActiveRoleAsync(request.RolesId, cancellationToken);

        await EnsureUsernameAvailableAsync(username, id, cancellationToken);
        await EnsureEmailAvailableAsync(email, id, cancellationToken);
        await EnsureLastActiveAdminRemainsAsync(user, role, request.IsActive, cancellationToken);

        user.Username = username;
        user.FullName = fullName;
        user.Email = email;
        user.Phone = phone;
        user.RolesId = role.Id;
        user.Role = role;
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.Now;

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            var salt = AuthPasswordHasher.CreateSalt();
            user.PasswordSalt = salt;
            user.PasswordHash = AuthPasswordHasher.HashPassword(request.Password.Trim(), salt);
        }

        await _users.SaveChangesAsync(cancellationToken);
        return ToResponse(user);
    }

    public async Task<UserResponse?> DeactivateUserAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdWithRoleAsync(id, asTracking: true, cancellationToken);
        if (user is null)
        {
            return null;
        }

        await EnsureLastActiveAdminRemainsAsync(user, user.Role, false, cancellationToken);

        user.IsActive = false;
        user.UpdatedAt = DateTime.Now;
        await _users.SaveChangesAsync(cancellationToken);

        return ToResponse(user);
    }

    internal static UserResponse ToResponse(AppUser user) =>
        new()
        {
            Id = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            RolesId = user.RolesId,
            Role = string.IsNullOrWhiteSpace(user.Role?.Name)
                ? AppUserRole.VIEWER.ToString()
                : user.Role.Name.Trim().ToUpperInvariant(),
            IsActive = user.IsActive,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };

    private async Task<AppRole> GetActiveRoleAsync(int rolesId, CancellationToken cancellationToken)
    {
        var role = await _users.GetActiveRoleAsync(rolesId, cancellationToken);
        return role ?? throw new ArgumentException("Selected role was not found or is inactive.");
    }

    private async Task EnsureUsernameAvailableAsync(
        string username,
        int? currentUserId,
        CancellationToken cancellationToken)
    {
        if (await _users.UsernameExistsAsync(username, currentUserId, cancellationToken))
        {
            throw new ArgumentException("Username already exists.");
        }
    }

    private async Task EnsureEmailAvailableAsync(
        string? email,
        int? currentUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        if (await _users.EmailExistsAsync(email, currentUserId, cancellationToken))
        {
            throw new ArgumentException("Email already exists.");
        }
    }

    private async Task EnsureLastActiveAdminRemainsAsync(
        AppUser user,
        AppRole? nextRole,
        bool nextIsActive,
        CancellationToken cancellationToken)
    {
        if (!user.IsActive || !IsAdmin(user.Role))
        {
            return;
        }

        if (nextIsActive && IsAdmin(nextRole))
        {
            return;
        }

        var otherActiveAdmins = await _users.CountOtherActiveAdminsAsync(user.Id, cancellationToken);
        if (otherActiveAdmins == 0)
        {
            throw new InvalidOperationException("At least one active admin user must remain.");
        }
    }

    private static bool IsAdmin(AppRole? role) =>
        string.Equals(role?.Name, AppUserRole.ADMIN.ToString(), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeRequired(string? value, string errorMessage)
    {
        var normalized = NormalizeOptional(value);
        return normalized ?? throw new ArgumentException(errorMessage);
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
