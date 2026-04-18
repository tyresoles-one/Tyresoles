namespace Tyresoles.Data.Features.RemoteAssist;

public sealed class RemoteAssistOptions
{
    public const string SectionName = "RemoteAssist";

    /// <summary>Session lifetime after create (minutes).</summary>
    public int SessionTimeoutMinutes { get; set; } = 120;

    /// <summary>Length of join code (alphanumeric).</summary>
    public int JoinCodeLength { get; set; } = 8;

    /// <summary>
    /// JWT <c>userType</c> values that may list active assist sessions and join as viewer without a join code.
    /// Case-insensitive. Combine with <see cref="AssistAdminUserIds"/> as needed.
    /// </summary>
    public List<string> AssistAdminUserTypes { get; set; } = new();

    /// <summary>
    /// User ids (<c>sub</c> / NameIdentifier) that may use assist admin APIs in addition to <see cref="AssistAdminUserTypes"/>.
    /// Case-insensitive.
    /// </summary>
    public List<string> AssistAdminUserIds { get; set; } = new();
}
