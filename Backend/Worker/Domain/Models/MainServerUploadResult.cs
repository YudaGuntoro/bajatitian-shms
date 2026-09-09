namespace Worker.Domain.Models;

public sealed record MainServerUploadResult(bool Success, string? Error = null)
{
    public static MainServerUploadResult Ok() => new(true);

    public static MainServerUploadResult Failed(string error) => new(false, error);
}
