using System;
using System.Linq;
using System.Linq.Expressions;

class Program
{
    static void Main()
    {
        string[] categories = new[] { ""CASING PROCUREMENT"" };
        Expression<Func<Vendor, bool>> query = v => categories.Contains(v.GroupCategory);
        var body = query.Body as MethodCallExpression;
        Console.WriteLine(""Method Name: "" + body.Method.Name);
        Console.WriteLine(""DeclaringType: "" + body.Method.DeclaringType);
        Console.WriteLine(""Is Extension: "" + (body.Object == null));
        Console.WriteLine(""Args count: "" + body.Arguments.Count);
    }
}
class Vendor {
    public string GroupCategory {get;set;}
}
