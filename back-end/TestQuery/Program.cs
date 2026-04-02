using System;
using System.Linq;
using System.Linq.Expressions;
using Tyresoles.Sql.Core.Query;
using Tyresoles.Sql.Abstractions;

class Program
{
    static void Main()
    {
        string[] categories = new[] { "CASING PROCUREMENT" };
        Expression<Func<Vendor, bool>> q = v => categories.Contains(v.GroupCategory);
        
        try {
            var ctx = new Tyresoles.Sql.Core.Query.ParameterContext();
            var visitor = new Tyresoles.Sql.Core.Query.SqlExpressionVisitor(ctx, new DialectFallback());
            visitor.Visit(q.Body);
            Console.WriteLine(visitor.GetSql());
            foreach(var kvp in ctx.Parameters) Console.WriteLine(kvp.Key + "=" + string.Join(",", (System.Collections.IEnumerable)kvp.Value));
        } catch (Exception ex) {
            Console.WriteLine(ex.ToString());
        }
    }
}
class Vendor {
    public string GroupCategory {get;set;} = "";
}
class DialectFallback : IDialect {
    public DbProvider Provider => DbProvider.SqlServer;
    public string QuoteIdentifier(string s) => s;
    public string BuildPagination(int? skip, int? take, string o) => "";
    public string BuildTableName(string t, string? c, string? s, bool i) => t;
    public string FormatParameterName(string n) => n;
}
