using Tyresoles.Data.Features.Accounts.Models;
using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Data.Features.Accounts
{
    public interface IAccountService
    {
        IQueryable<GLAccount> GetGLAccounts(ITenantScope scope);
    }
}
