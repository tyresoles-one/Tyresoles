using System;
using System.Linq;
using System.Linq.Expressions;
using Tyresoles.Sql.Core.Query;

class Program
{
    static void Main()
    {
        string[] categories = new[] { ""CASING PROCUREMENT"" };
        Expression<Func<Vendor, bool>> q = v => categories.Contains(v.GroupCategory);
        var qry = new Query<Vendor>(new Tyresoles.Sql.Abstractions.TenantScope(new Tyresoles.Sql.Abstractions.DialectFallback(), """", null), ""Vendor"", """");
        try {
            var res = qry.Where(q).Build();
            Console.WriteLine(res.Sql);
        } catch (Exception ex) {
            Console.WriteLine(ex.ToString());
        }
    }
}
class Vendor {
    public string GroupCategory {get;set;}
}
namespace Tyresoles.Sql.Abstractions {
    public class TenantScope {
        public IDialect Dialect {get;}
        public string CurrentCompany {get;}
        public string SchemaPrefix {get;}
        public TenantScope(IDialect d, string c, string s) { Dialect=d; CurrentCompany=c; SchemaPrefix=s; }
    }
    public interface IDialect {
        DbProvider Provider {get;}
        string QuoteIdentifier(string s);
        string BuildPagination(int? skip, int? take, string orderByClause);
        string BuildTableName(string tableName, string company, string schemaPrefix, bool isShared);
        string FormatParameterName(string name);
    }
    public enum DbProvider { SqlServer }
    public class DialectFallback : IDialect {
        public DbProvider Provider => DbProvider.SqlServer;
        public string QuoteIdentifier(string s) => s;
        public string BuildPagination(int? skip, int? take, string o) => """";
        public string BuildTableName(string t, string c, string s, bool i) => t;
        public string FormatParameterName(string n) => n;
    }
}
