using System.Security.Cryptography;
using System.Text;

namespace Tyresoles.Protean.Encryption;

/// <summary>
/// Default implementation – pure BCL, no external crypto libs.
/// Registered as singleton; all methods are thread-safe.
/// AES symmetric paths match Tyresoles.Live <c>Encryptor.EncryptBySymmetricKey</c> /
/// <c>DecryptBySymmerticKeyBytes</c> (AES-256-ECB, PKCS7).
/// </summary>
public sealed class ProteanEncryptor : IProteanEncryptor
{
    // ---------------------------------------------------------------
    // Key generation
    // ---------------------------------------------------------------

    public byte[] GenerateSecureKey()
    {
        using var aes = Aes.Create();
        aes.KeySize = 256;
        return aes.Key;
    }

    // ---------------------------------------------------------------
    /// <summary>
    /// Encrypt <paramref name="plaintext"/> using the public key.
    /// Legacy implementation used RSACryptoServiceProvider with fOAEP: false.
    /// </summary>
    public string EncryptRsa(string plaintext, string publicKey)
    {
        // Legacy Tyresoles.Live uses RSACryptoServiceProvider.Encrypt(bytes, false) 
        // which is PKCS#1 v1.5 padding.
        byte[] keyBytes = Convert.FromBase64String(publicKey);
        byte[] dataBytes = Encoding.UTF8.GetBytes(plaintext);

        // Mimic legacy Encryptor.Encrypt logic
        // It used BouncyCastle to parse the DER key into RSAParameters
        var keyParameter = Org.BouncyCastle.Security.PublicKeyFactory.CreateKey(keyBytes);
        var rsaKeyParameters = (Org.BouncyCastle.Crypto.Parameters.RsaKeyParameters)keyParameter;
        
        RSAParameters rsaParameters = new RSAParameters
        {
            Modulus = rsaKeyParameters.Modulus.ToByteArrayUnsigned(),
            Exponent = rsaKeyParameters.Exponent.ToByteArrayUnsigned()
        };

        using var rsa = new RSACryptoServiceProvider();
        rsa.ImportParameters(rsaParameters);
        
        // fOAEP: false is critical for legacy compatibility with NIC IRP
        byte[] encryptedBytes = rsa.Encrypt(dataBytes, false);
        return Convert.ToBase64String(encryptedBytes);
    }

    // ---------------------------------------------------------------
    // AES-256-ECB (Protean / NIC – same as Tyresoles.Live Encryptor.EncryptBySymmetricKey)
    // ---------------------------------------------------------------

    /// <inheritdoc />
    public string EncryptAes(byte[] data, byte[] key)
        => EncryptBySymmetricKey(data, key);

    /// <summary>
    /// Bit-for-bit equivalent to Tyresoles.Live <c>Utilities.Encryptor.EncryptBySymmetricKey</c>
    /// (replaces legacy <c>AesManaged</c> with <see cref="Aes.Create()"/>, same parameters).
    /// </summary>
    private static string EncryptBySymmetricKey(byte[] dataToEncrypt, byte[] keyBytes)
    {
        ArgumentNullException.ThrowIfNull(dataToEncrypt);
        ArgumentNullException.ThrowIfNull(keyBytes);

        try
        {
            using var aes = Aes.Create();
            aes.KeySize   = 256;
            aes.BlockSize = 128;
            aes.Key       = keyBytes;
            aes.Mode      = CipherMode.ECB;
            aes.Padding   = PaddingMode.PKCS7;
            using ICryptoTransform encrypt = aes.CreateEncryptor();
            byte[] cipher = encrypt.TransformFinalBlock(dataToEncrypt, 0, dataToEncrypt.Length);
            return Convert.ToBase64String(cipher);
        }
        catch (CryptographicException ex)
        {
            throw new CryptographicException("AES-ECB encrypt failed (Protean symmetric key).", ex);
        }
    }

    public string DecryptAes(string base64Ciphertext, byte[] key)
        => Encoding.UTF8.GetString(DecryptAesBytes(base64Ciphertext, key));

    public byte[] DecryptAesBytes(string base64Ciphertext, byte[] key)
        => DecryptBySymmetricKeyBytes(base64Ciphertext, key);

    /// <summary>
    /// Equivalent to Tyresoles.Live <c>Utilities.Encryptor.DecryptBySymmerticKeyBytes</c> (raw plaintext bytes).
    /// </summary>
    private static byte[] DecryptBySymmetricKeyBytes(string encryptedText, byte[] keyBytes)
    {
        ArgumentNullException.ThrowIfNull(encryptedText);
        ArgumentNullException.ThrowIfNull(keyBytes);

        try
        {
            byte[] dataToDecrypt = Convert.FromBase64String(encryptedText);
            using var aes = Aes.Create();
            aes.KeySize   = 256;
            aes.BlockSize = 128;
            aes.Key       = keyBytes;
            aes.Mode      = CipherMode.ECB;
            aes.Padding   = PaddingMode.PKCS7;
            using ICryptoTransform decrypt = aes.CreateDecryptor();
            return decrypt.TransformFinalBlock(dataToDecrypt, 0, dataToDecrypt.Length);
        }
        catch (CryptographicException ex)
        {
            throw new CryptographicException("AES-ECB decrypt failed (Protean symmetric key).", ex);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("Invalid base-64 ciphertext for AES decrypt.", ex);
        }
    }

    // ---------------------------------------------------------------
    // RSA-SHA256 signing (for X-Asp-Auth-Signature header)
    // ---------------------------------------------------------------

    public string SignData(byte[] data, string privateKeyXml)
    {
        // Legacy Tyresoles.Live uses RSACryptoServiceProvider.SignData(data, new SHA256CryptoServiceProvider())
        using var rsa = new RSACryptoServiceProvider();
        rsa.FromXmlString(privateKeyXml);
        var signature = rsa.SignData(data, new SHA256CryptoServiceProvider());
        return Convert.ToBase64String(signature);
    }

    // ---------------------------------------------------------------
    // Convenience
    // ---------------------------------------------------------------

    public string Base64Decode(string base64)
        => Encoding.UTF8.GetString(Convert.FromBase64String(base64));
}
