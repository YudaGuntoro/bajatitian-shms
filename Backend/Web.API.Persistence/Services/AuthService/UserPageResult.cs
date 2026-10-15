using Web.API.Domain.Auth;

namespace Web.API.Persistence.Services.AuthService;

public sealed class UserPageResult
{
    public IReadOnlyList<UserResponse> Users { get; init; } = [];

    public int Page { get; init; }

    public int Limit { get; init; }

    public int Total { get; init; }

    public int TotalPage => Math.Max(1, (int)Math.Ceiling(Total / (double)Limit));
}
