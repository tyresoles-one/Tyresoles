namespace Tyresoles.Easebuzz.Models;

/// <summary>
/// Input for initiating a payment. Hash is computed by the service; do not pass Key/Salt from client.
/// </summary>
public sealed class InitiatePaymentInput
{
    public string TxnId { get; set; } = string.Empty;
    public string Amount { get; set; } = string.Empty;
    public string ProductInfo { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string SuccessUrl { get; set; } = string.Empty;
    public string FailureUrl { get; set; } = string.Empty;
}
