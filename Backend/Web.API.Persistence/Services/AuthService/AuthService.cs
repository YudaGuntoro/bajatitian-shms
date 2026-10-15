using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Web.API.Domain.Auth;
using Web.API.Persistence.Repositories.Auth;
using Web.API.Persistence.Services.Shared;

namespace Web.API.Persistence.Services.AuthService;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly JwtSettings _jwtSettings;

    public AuthService(IUserRepository users, IOptions<JwtSettings> jwtOptions)
    {
        _users = users;
        _jwtSettings = jwtOptions.Value;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new UnauthorizedAccessException("Invalid username or password.");
        }

        var username = request.Username.Trim();
        var user = await _users.GetByUsernameWithRoleAsync(username, asTracking: true);
        if (user == null || !AuthPasswordHasher.VerifyPassword(request.Password, user.PasswordSalt, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid username or password.");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("User is inactive.");
        }

        if (user.Role is null || !user.Role.IsActive)
        {
            throw new UnauthorizedAccessException("User role is inactive.");
        }

        var loginAt = DateTime.Now;
        var expiresAt = loginAt.AddHours(_jwtSettings.ExpiresHours);
        user.LastLoginAt = loginAt;
        user.UpdatedAt = loginAt;
        await _users.SaveChangesAsync();

        return new LoginResponse
        {
            AccessToken = CreateJwt(user, loginAt, expiresAt),
            TokenType = "Bearer",
            ExpiresAt = expiresAt,
            User = UserManagementService.ToResponse(user)
        };
    }

    private string CreateJwt(AppUser user, DateTime issuedAt, DateTime expiresAt)
    {
        var roleName = GetRoleName(user);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(issuedAt).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, roleName),
            new("username", user.Username),
            new("full_name", user.FullName),
            new("role", roleName),
            new("roles_id", user.RolesId.ToString())
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
        }

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            notBefore: issuedAt,
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GetRoleName(AppUser user) =>
        string.IsNullOrWhiteSpace(user.Role?.Name)
            ? AppUserRole.VIEWER.ToString()
            : user.Role.Name.Trim().ToUpperInvariant();
}
