namespace Tyresoles.Data.Features.Admin.EmailAccounts;

public interface IRediffmailService
{
    Task<AdminAuthResult> AuthenticateAsync(CancellationToken cancellationToken = default);
    Task<EmailOperationResult> CreateUserAsync(CreateEmailAccountInput input, CancellationToken cancellationToken = default);
    Task<EmailOperationResult> UpdateUserAsync(UpdateEmailAccountInput input, CancellationToken cancellationToken = default);
    Task<EmailOperationResult> DeleteUserAsync(string userEmail, CancellationToken cancellationToken = default);
    Task<EmailOperationResult> ChangePasswordAsync(ChangeEmailPasswordInput input, CancellationToken cancellationToken = default);
    Task<EmailOperationResult> UpdateUserStatusAsync(UpdateEmailStatusInput input, CancellationToken cancellationToken = default);
    Task<RediffUserContactDto?> GetUserDetailsAsync(string? userId, string? employeeCode, CancellationToken cancellationToken = default);
    Task<EmailOperationResult> AddGlobalAddressContactAsync(AddGlobalContactInput input, CancellationToken cancellationToken = default);
    Task<GlobalAddressBookResult> GetGlobalAddressContactAsync(CancellationToken cancellationToken = default);
}
