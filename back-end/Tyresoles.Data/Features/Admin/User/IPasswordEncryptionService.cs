namespace Tyresoles.Data.Features.Admin.User;

public interface IPasswordEncryptionService
{
    string CreateSalt();
    string EncryptPassword(string password, string salt);
    bool IsPasswordMatch(string password, string salt, string passwordEncrypt);
}
