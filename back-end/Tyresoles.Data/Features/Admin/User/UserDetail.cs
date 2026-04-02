namespace Tyresoles.Data.Features.Admin.User;

public sealed class UserDetail
{
    public string UserId { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? RDPPassword { get; init; }
    public string? NavConfigName { get; init; }
}
