using HotChocolate;
using HotChocolate.Types;
using Tyresoles.Data.Features.Admin.EmailAccounts;

namespace Tyresoles.Web.Features.EmailAccounts;

[ExtendObjectType(typeof(Query))]
public class EmailAccountQueryExtension
{
    [GraphQLName("emailAccounts")]
    public async Task<GlobalAddressBookResult> GetEmailAccounts(
        [Service] IRediffmailService rediffmailService,
        CancellationToken cancellationToken)
    {
        return await rediffmailService.GetGlobalAddressContactAsync(cancellationToken);
    }

    [GraphQLName("emailAccountDetails")]
    public async Task<RediffUserContactDto?> GetEmailAccountDetails(
        string? userId,
        string? employeeCode,
        [Service] IRediffmailService rediffmailService,
        CancellationToken cancellationToken)
    {
        return await rediffmailService.GetUserDetailsAsync(userId, employeeCode, cancellationToken);
    }

    [GraphQLName("globalAddressBook")]
    public async Task<GlobalAddressBookResult> GetGlobalAddressBook(
        [Service] IRediffmailService rediffmailService,
        CancellationToken cancellationToken)
    {
        return await rediffmailService.GetGlobalAddressContactAsync(cancellationToken);
    }
}

[ExtendObjectType(typeof(Mutation))]
public class EmailAccountMutationExtension
{
    [GraphQLName("createEmailAccount")]
    public async Task<EmailOperationResult> CreateEmailAccount(
        CreateEmailAccountInput input,
        [Service] IRediffmailService rediffmailService,
        CancellationToken cancellationToken)
    {
        return await rediffmailService.CreateUserAsync(input, cancellationToken);
    }

    [GraphQLName("updateEmailAccount")]
    public async Task<EmailOperationResult> UpdateEmailAccount(
        UpdateEmailAccountInput input,
        [Service] IRediffmailService rediffmailService,
        CancellationToken cancellationToken)
    {
        return await rediffmailService.UpdateUserAsync(input, cancellationToken);
    }

    [GraphQLName("deleteEmailAccount")]
    public async Task<EmailOperationResult> DeleteEmailAccount(
        string userEmail,
        [Service] IRediffmailService rediffmailService,
        CancellationToken cancellationToken)
    {
        return await rediffmailService.DeleteUserAsync(userEmail, cancellationToken);
    }

    [GraphQLName("changeEmailAccountPassword")]
    public async Task<EmailOperationResult> ChangeEmailAccountPassword(
        ChangeEmailPasswordInput input,
        [Service] IRediffmailService rediffmailService,
        CancellationToken cancellationToken)
    {
        return await rediffmailService.ChangePasswordAsync(input, cancellationToken);
    }

    [GraphQLName("updateEmailAccountStatus")]
    public async Task<EmailOperationResult> UpdateEmailAccountStatus(
        UpdateEmailStatusInput input,
        [Service] IRediffmailService rediffmailService,
        CancellationToken cancellationToken)
    {
        return await rediffmailService.UpdateUserStatusAsync(input, cancellationToken);
    }

    [GraphQLName("addGlobalAddressContact")]
    public async Task<EmailOperationResult> AddGlobalAddressContact(
        AddGlobalContactInput input,
        [Service] IRediffmailService rediffmailService,
        CancellationToken cancellationToken)
    {
        return await rediffmailService.AddGlobalAddressContactAsync(input, cancellationToken);
    }
}
