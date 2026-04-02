using System;
using System.Linq;
using System.Linq.Expressions;
class P {
    public static void Main() {
        string[] categories = new[] { ""foo"" };
        Expression<Func<Vendor, bool>> q = v => categories.Contains(v.Category);
        var mc = (MethodCallExpression)q.Body;
        var arg0 = mc.Arguments[0];
        Console.WriteLine(arg0.NodeType);
        if (arg0 is MemberExpression me) {
            Console.WriteLine(""Expr: "" + me.Expression.NodeType);
        }
    }
}
class Vendor { public string Category {get;set;} }
