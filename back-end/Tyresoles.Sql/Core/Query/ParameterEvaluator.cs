using System.Linq.Expressions;
using System.Reflection;

namespace Tyresoles.Sql.Core.Query;

/// <summary>
/// Fast evaluation of Expression trees without calling slow Expression.Compile().
/// Handles common patterns like constants, closure variables, and simple properties.
/// </summary>
internal static class ParameterEvaluator
{
    public static object? Evaluate(Expression? node)
    {
        if (node == null) return null;

        switch (node.NodeType)
        {
            case ExpressionType.Constant:
                return ((ConstantExpression)node).Value;

            case ExpressionType.MemberAccess:
                var me = (MemberExpression)node;
                var container = Evaluate(me.Expression);
                
                if (me.Member is FieldInfo f) return f.GetValue(container);
                if (me.Member is PropertyInfo p) return p.GetValue(container);
                break;

            case ExpressionType.Convert:
                return Evaluate(((UnaryExpression)node).Operand);

            case ExpressionType.NewArrayInit:
                var newArray = (NewArrayExpression)node;
                var arr = Array.CreateInstance(newArray.Type.GetElementType()!, newArray.Expressions.Count);
                for (int i = 0; i < newArray.Expressions.Count; i++)
                    arr.SetValue(Evaluate(newArray.Expressions[i]), i);
                return arr;
            
            case ExpressionType.ListInit:
                var listInit = (ListInitExpression)node;
                var list = (System.Collections.IList)Activator.CreateInstance(listInit.Type)!;
                foreach (var init in listInit.Initializers)
                    list.Add(Evaluate(init.Arguments[0]));
                return list;

            case ExpressionType.Call:
                var mc = (MethodCallExpression)node;
                var obj = mc.Object != null ? Evaluate(mc.Object) : null;
                var args = new object?[mc.Arguments.Count];
                for (int i = 0; i < mc.Arguments.Count; i++) args[i] = Evaluate(mc.Arguments[i]);
                return mc.Method.Invoke(obj, args);

            case ExpressionType.Invoke:
                var inv = (InvocationExpression)node;
                var funcObj = Evaluate(inv.Expression);
                if (funcObj is Delegate del)
                {
                    var invArgs = new object?[inv.Arguments.Count];
                    for (int i = 0; i < inv.Arguments.Count; i++) invArgs[i] = Evaluate(inv.Arguments[i]);
                    return del.DynamicInvoke(invArgs);
                }
                break;

            case ExpressionType.Parameter:
                // Closure variables (e.g. list in list.Contains(x)) cannot be evaluated without compiling,
                // and compiling triggers InvalidProgramException in some runtimes. Callers must use
                // raw Where(string, object) with explicit parameters instead of expression-based Contains(capture).
                throw new InvalidOperationException(
                    "Cannot evaluate closure parameter in expression tree. Use Where(string sql, object parameters) with explicit values instead of Contains(capturedVariable).");
        }

        // Fallback for complex expressions - only then compile
        var objectMember = Expression.Convert(node, typeof(object));
        var getterLambda = Expression.Lambda<Func<object?>>(objectMember);
        return getterLambda.Compile()();
    }
}
