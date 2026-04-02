using Microsoft.Extensions.Options;
using Tyresoles.Easebuzz.Hash;
using Tyresoles.Easebuzz.Http;
using Tyresoles.Easebuzz.Models;

namespace Tyresoles.Easebuzz.Services;

/// <summary>
/// Easebuzz payment service: initiates payments (with server-side hash) and fetches transaction status.
/// </summary>
public sealed class EasebuzzPaymentService : IEasebuzzPaymentService
{
    private readonly IEasebuzzPaymentClient _client;
    private readonly IEasebuzzHashProvider _hashProvider;
    private readonly EasebuzzOptions _options;

    public EasebuzzPaymentService(
        IEasebuzzPaymentClient client,
        IEasebuzzHashProvider hashProvider,
        IOptions<EasebuzzOptions> options)
    {
        _client = client;
        _hashProvider = hashProvider;
        _options = options.Value;
    }

    public async Task<InitiatePaymentResult> InitiatePaymentAsync(InitiatePaymentInput input, CancellationToken cancellationToken = default)
    {
        var hash = _hashProvider.ComputeInitiatePaymentHash(
            _options.Key,
            _options.Salt,
            input.TxnId,
            input.Amount,
            input.ProductInfo,
            input.FirstName,
            input.Email);

        var request = new InitiatePaymentRequest
        {
            Key = _options.Key,
            TxnId = input.TxnId,
            Amount = input.Amount,
            ProductInfo = input.ProductInfo,
            FirstName = input.FirstName,
            Email = input.Email,
            Phone = input.Phone,
            SuccessUrl = input.SuccessUrl,
            FailureUrl = input.FailureUrl,
            Hash = hash,
        };

        var result = await _client.InitiatePaymentAsync(request, cancellationToken).ConfigureAwait(false);
        return result ?? new InitiatePaymentResult();
    }

    public Task<TransactionStatusResult?> GetTransactionStatusAsync(string txnIdOrRef, CancellationToken cancellationToken = default)
        => _client.GetTransactionStatusAsync(txnIdOrRef, cancellationToken);

    public bool VerifyWebhookSignature(string payload, string receivedHash)
    {
        if (string.IsNullOrEmpty(receivedHash))
            return false;
        // Easebuzz webhook verification: confirm algorithm from https://docs.easebuzz.in/docs/payment-gateway/ (Transaction Webhook).
        // Placeholder: many gateways use SHA512(payload + salt) or SHA512(key|field1|...|salt). Compare with receivedHash.
        var computed = ComputeWebhookHash(payload);
        return string.Equals(computed, receivedHash, StringComparison.OrdinalIgnoreCase);
    }

    private string ComputeWebhookHash(string payload)
    {
        var input = payload + _options.Salt;
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = System.Security.Cryptography.SHA512.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
