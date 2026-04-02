using Tyresoles.Easebuzz.Models;

namespace Tyresoles.Easebuzz;

/// <summary>
/// High-level Easebuzz payment service. Inject this where you need to initiate payments or check transaction status.
/// </summary>
public interface IEasebuzzPaymentService
{
    /// <summary>
    /// Initiates a payment: computes hash server-side and calls Easebuzz API; returns result with access_key for payment UI.
    /// </summary>
    Task<InitiatePaymentResult> InitiatePaymentAsync(InitiatePaymentInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches transaction status by transaction ID or reference. Returns null if not found or on error.
    /// </summary>
    Task<TransactionStatusResult?> GetTransactionStatusAsync(string txnIdOrRef, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies webhook signature (hash) against payload. Use when processing Easebuzz callbacks.
    /// Confirm verification algorithm from https://docs.easebuzz.in/docs/payment-gateway/
    /// </summary>
    bool VerifyWebhookSignature(string payload, string receivedHash);
}
