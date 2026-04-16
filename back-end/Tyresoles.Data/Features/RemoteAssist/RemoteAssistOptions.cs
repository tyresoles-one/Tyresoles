namespace Tyresoles.Data.Features.RemoteAssist;

public sealed class RemoteAssistOptions
{
    public const string SectionName = "RemoteAssist";

    /// <summary>Session lifetime after create (minutes).</summary>
    public int SessionTimeoutMinutes { get; set; } = 120;

    /// <summary>Length of join code (alphanumeric).</summary>
    public int JoinCodeLength { get; set; } = 8;
}
