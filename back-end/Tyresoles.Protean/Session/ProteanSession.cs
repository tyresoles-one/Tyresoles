using System.Text.Json;

namespace Tyresoles.Protean.Session;

/// <summary>Cached Protean GSP session (per GSTIN).</summary>
public sealed record ProteanSession
{
    public required string Gstin     { get; init; }
    public required string Username  { get; init; }
    public required string Password  { get; init; }
    public required string AuthToken { get; init; }
    public required string AppKey    { get; init; }
    /// <summary>Session Encryption Key (base-64 AES-256).</summary>
    public required string Sek       { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }

    public bool IsAlive => DateTimeOffset.UtcNow < ExpiresAt;
    public override string ToString()
    {
        return JsonSerializer.Serialize(this);
    }
}
