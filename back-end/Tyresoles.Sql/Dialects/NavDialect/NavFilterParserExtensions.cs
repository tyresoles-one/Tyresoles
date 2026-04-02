using System;
using System.Text.RegularExpressions;
using Tyresoles.Sql.Abstractions;
using Tyresoles.Sql.Core.Query;

namespace Tyresoles.Sql.Dialects.NavDialect;

public static class NavFilterParserExtensions
{
    /// <summary>
    /// Seamlessly translates NAV native expression syntaxes e.g. "BLOCKED=FILTER(<>1)" into typed raw SQL conditions.
    /// </summary>
    public static IQuery<T> Filter<T>(this IQuery<T> query, string navFilterStr) where T : class
    {
        var match = Regex.Match(navFilterStr, @"(.+)=FILTER\((.+)\)", RegexOptions.IgnoreCase);
        if (!match.Success) throw new ArgumentException($"Invalid NAV Filter Syntax: {navFilterStr}");

        var columnName = match.Groups[1].Value.Trim();
        var conditionStr = match.Groups[2].Value.Trim(); // "<>1"

        string sqlOperator = "=";
        string valueStr = conditionStr;

        if (conditionStr.StartsWith("<>")) { sqlOperator = "<>"; valueStr = conditionStr.Substring(2); }
        else if (conditionStr.StartsWith(">=")) { sqlOperator = ">="; valueStr = conditionStr.Substring(2); }
        else if (conditionStr.StartsWith("<=")) { sqlOperator = "<="; valueStr = conditionStr.Substring(2); }
        else if (conditionStr.StartsWith(">")) { sqlOperator = ">"; valueStr = conditionStr.Substring(1); }
        else if (conditionStr.StartsWith("<")) { sqlOperator = "<"; valueStr = conditionStr.Substring(1); }

        var pId = Guid.NewGuid().ToString("N");
        var sqlStr = $"[{columnName}] {sqlOperator} @{pId}";
        
        if (query is Query<T> customQuery)
        {
            var pObj = new System.Collections.Generic.Dictionary<string, object>();
            pObj[pId] = int.TryParse(valueStr, out var v) ? (object)v : valueStr;
            return customQuery.Where(sqlStr, pObj);
        }
        
        throw new NotSupportedException("Filter extensions explicitly require Tyresoles.Sql.Core.Query.Query<T> base resolution.");
    }
}
