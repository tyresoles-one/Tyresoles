using Tyresoles.Easebuzz.Models;

namespace Tyresoles.Easebuzz.Http;

/// <summary>
/// Typed HTTP client for Easebuzz payment APIs. Used by IEasebuzzPaymentService.
/// </summary>
public interface IEasebuzzPaymentClient
{
    /// <summary>
    /// POST to initiate payment; returns result containing access_key or data.
    /// </summary>
    Task<InitiatePaymentResult?> InitiatePaymentAsync(InitiatePaymentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches transaction status by transaction ID or reference.
    /// </summary>
    Task<TransactionStatusResult?> GetTransactionStatusAsync(string txnIdOrRef, CancellationToken cancellationToken = default);
}
