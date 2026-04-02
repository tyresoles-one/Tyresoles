namespace Tyresoles.Data.Features.Admin.User;

/// <summary>Lightweight user record returned by the attendee search query.</summary>
public class UserSearchResult
{
    public string UserId   { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty;
    public int?   Avatar   { get; set; }
}
