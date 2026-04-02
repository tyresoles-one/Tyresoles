using System.Security.Cryptography;
using System.Text;

namespace Tyresoles.Data.Features.Admin.User;

public sealed class PasswordEncryptionService : IPasswordEncryptionService
{
    public string CreateSalt()
    {
        var data = new byte[0x10];
        RandomNumberGenerator.Fill(data);
        return Convert.ToBase64String(data);
    }

    public string EncryptPassword(string password, string salt)
    {
        using var sha256 = SHA256.Create();
        var saltedPassword = string.Format("{0}{1}", salt, password);
        byte[] saltedPasswordAsBytes = Encoding.UTF8.GetBytes(saltedPassword);
        return Convert.ToBase64String(sha256.ComputeHash(saltedPasswordAsBytes));
    }

    public bool IsPasswordMatch(string password, string salt, string passwordEncrypt)
    {
        if (password.IsEmpty() || salt.IsEmpty() || passwordEncrypt.IsEmpty())
            return false;
        var encPass = EncryptPassword(password, salt);
        return string.Equals(passwordEncrypt, encPass, StringComparison.Ordinal);
    }

}
