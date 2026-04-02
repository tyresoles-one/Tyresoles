using System.Security.Cryptography;
using System.Text;

namespace Tyresoles.Easebuzz.Hash;

/// <summary>
/// SHA-512 hash for Easebuzz request signing. Stateless and thread-safe.
/// Field order per Easebuzz/PayU-style: key|txnid|amount|productinfo|firstname|email|||||||||||SALT
/// Confirm from https://docs.easebuzz.in/docs/payment-gateway/
/// </summary>
public sealed class EasebuzzHashProvider : IEasebuzzHashProvider
{
    public string ComputeInitiatePaymentHash(string key, string salt, string txnId, string amount, string productInfo, string firstname, string email)
    {
        // Pipe-separated string; empty fields between email and salt per PayU/Easebuzz convention
        var sb = new StringBuilder(256);
        sb.Append(key);
        sb.Append('|').Append(txnId);
        sb.Append('|').Append(amount);
        sb.Append('|').Append(productInfo);
        sb.Append('|').Append(firstname);
        sb.Append('|').Append(email);
        sb.Append("|||||||||||||");
        sb.Append(salt);

        var input = sb.ToString();
        return ComputeSha512Hex(input);
    }

    private static string ComputeSha512Hex(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA512.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
