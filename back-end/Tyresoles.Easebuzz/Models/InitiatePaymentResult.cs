using System.Text.Json.Serialization;

namespace Tyresoles.Easebuzz.Models;

/// <summary>
/// Response from Easebuzz Initiate Payment API. Contains access_key used to open payment UI.
/// </summary>
public sealed class InitiatePaymentResult
{
    [JsonPropertyName("data")]
    public string? Data { get; set; }

    [JsonPropertyName("access_key")]
    public string? AccessKey { get; set; }

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("result")]
    public string? Result { get; set; }
}
