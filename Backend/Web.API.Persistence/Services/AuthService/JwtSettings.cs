namespace Web.API.Persistence.Services.AuthService;

public class JwtSettings
{
    public string Issuer { get; set; } = "SHMSSystem";
    public string Audience { get; set; } = "SHMSSystem.Frontend";
    public string SigningKey { get; set; } = "SHMSSystem-Development-Jwt-Signing-Key-2026-Change-Me";
    public int ExpiresHours { get; set; } = 8;
}
