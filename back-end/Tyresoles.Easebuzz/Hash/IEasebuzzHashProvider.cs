namespace Tyresoles.Easebuzz.Hash;

/// <summary>
/// Stateless, thread-safe provider for Easebuzz request hash (SHA-512).
/// Salt is never exposed; used only when computing the hash server-side.
/// </summary>
public interface IEasebuzzHashProvider
{
    /// <summary>
    /// Computes the payment initiation hash. Field order must match Easebuzz docs.
    /// See https://docs.easebuzz.in/docs/payment-gateway/ (Initiate Payment API).
    /// Formula: sha512(key|txnid|amount|productinfo|firstname|email|||||||||||salt).
    /// </summary>
    string ComputeInitiatePaymentHash(string key, string salt, string txnId, string amount, string productInfo, string firstname, string email);
}
