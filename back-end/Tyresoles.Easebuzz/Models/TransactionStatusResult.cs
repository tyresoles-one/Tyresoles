using System.Text.Json.Serialization;

namespace Tyresoles.Easebuzz.Models;

/// <summary>
/// Transaction status from Easebuzz Transaction API. Confirm field names from https://docs.easebuzz.in/docs/payment-gateway/
/// </summary>
public sealed class TransactionStatusResult
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("txnid")]
    public string? TxnId { get; set; }

    [JsonPropertyName("amount")]
    public string? Amount { get; set; }

    [JsonPropertyName("firstname")]
    public string? FirstName { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("productinfo")]
    public string? ProductInfo { get; set; }

    [JsonPropertyName("mode")]
    public string? Mode { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("error_Message")]
    public string? ErrorMessage { get; set; }
}
