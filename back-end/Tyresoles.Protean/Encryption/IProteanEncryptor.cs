namespace Tyresoles.Protean.Encryption;

/// <summary>All cryptographic operations required by the Protean GSP API.</summary>
public interface IProteanEncryptor
{
    /// <summary>Generate a random 32-byte AES app key.</summary>
    byte[] GenerateSecureKey();

    /// <summary>
    /// Encrypt <paramref name="plaintext"/> using the public key.
    /// Legacy implementation used RSACryptoServiceProvider with fOAEP: false.
    /// </summary>
    string EncryptRsa(string plaintext, string publicKey);

    /// <summary>
    /// AES-256-ECB encrypt <paramref name="data"/> bytes using <paramref name="key"/> bytes.
    /// Returns base-64 ciphertext string (as expected by the Protean API).
    /// </summary>
    string EncryptAes(byte[] data, byte[] key);

    /// <summary>
    /// AES-256-ECB decrypt <paramref name="base64Ciphertext"/> using <paramref name="key"/> bytes.
    /// Returns decrypted UTF-8 string.
    /// </summary>
    string DecryptAes(string base64Ciphertext, byte[] key);

    /// <summary>
    /// AES-256-ECB decrypt returning raw bytes (for cases where the inner payload is JSON).
    /// </summary>
    byte[] DecryptAesBytes(string base64Ciphertext, byte[] key);

    /// <summary>
    /// RSA-SHA256 sign <paramref name="data"/> using the ASP private key XML.
    /// Returns base-64 signature.
    /// </summary>
    string SignData(byte[] data, string privateKeyXml);

    /// <summary>Base-64 decode helper (convenience).</summary>
    string Base64Decode(string base64);
}
