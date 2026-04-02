using System.Text.Json.Serialization;

namespace Tyresoles.Easebuzz.Models;

/// <summary>
/// Request to initiate a payment. Field names aligned with Easebuzz Initiate Payment API where applicable.
/// See https://docs.easebuzz.in/docs/payment-gateway/
/// </summary>
public sealed class InitiatePaymentRequest
{
    [JsonPropertyName("txnid")]
    public string TxnId { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public string Amount { get; set; } = string.Empty;

    [JsonPropertyName("productinfo")]
    public string ProductInfo { get; set; } = string.Empty;

    [JsonPropertyName("firstname")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("surl")]
    public string SuccessUrl { get; set; } = string.Empty;

    [JsonPropertyName("furl")]
    public string FailureUrl { get; set; } = string.Empty;

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    /// <summary>Hash generated server-side (key|txnid|amount|productinfo|firstname|email|...|salt). Never send salt to client.</summary>
    [JsonPropertyName("hash")]
    public string Hash { get; set; } = string.Empty;
}
