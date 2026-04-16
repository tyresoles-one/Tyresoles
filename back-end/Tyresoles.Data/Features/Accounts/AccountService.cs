using Tyresoles.Data.Features.Accounts.Models;
using Tyresoles.Sql.Abstractions;
using Tyresoles.Sql.GraphQL;

namespace Tyresoles.Data.Features.Accounts
{
    public sealed class AccountService : IAccountService
    {
        public IQueryable<GLAccount> GetGLAccounts(ITenantScope scope)
        {
            return scope.Query<GLAccount>()
                .Where(x => x.AccountType == 0) // Posting accounts only
                .OrderBy(x => x.No)
                .AsQueryable(scope);
        }

        IQueryable<GLAccount> IAccountService.GetGLAccounts(ITenantScope scope)
        {
            return GetGLAccounts(scope);
        }
    }
}
