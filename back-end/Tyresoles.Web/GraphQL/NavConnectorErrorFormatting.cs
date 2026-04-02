using System.ServiceModel;

namespace Tyresoles.Web.GraphQL;

/// <summary>
/// Extracts user-relevant text from NAV SOAP/WCF failures for GraphQL <see cref="IError"/> messages.
/// </summary>
public static class NavConnectorErrorFormatting
{
    private static readonly string[] GenericTopMessages =
    [
        "Unexpected Execution Error",
        "Unexpected Execution Error.",
        "Internal Server Error",
    ];

    /// <summary>Hot Chocolate / SOAP often use a generic outer fault string; prefer inner or <see cref="FaultException.Reason"/>.</summary>
    public static string FormatMessage(Exception ex)
    {
        if (ex is AggregateException agg && agg.InnerExceptions.Count == 1)
            ex = agg.InnerExceptions[0];

        if (ex is FaultException fe)
        {
            var reason = fe.Reason?.ToString()?.Trim();
            if (!string.IsNullOrWhiteSpace(reason) && !IsGenericTopMessage(reason))
                return reason!;

            var fm = fe.Message?.Trim();
            if (!string.IsNullOrWhiteSpace(fm) && !IsGenericTopMessage(fm))
                return fm!;
        }

        Exception? cur = ex;
        while (cur?.InnerException != null)
            cur = cur.InnerException;
        var leaf = cur?.Message?.Trim();
        if (!string.IsNullOrWhiteSpace(leaf) && !IsGenericTopMessage(leaf))
            return leaf!;

        var root = ex.Message?.Trim();
        if (!string.IsNullOrWhiteSpace(root))
            return root!;
        return "NAV connector call failed.";
    }

    private static bool IsGenericTopMessage(string m) =>
        GenericTopMessages.Contains(m, StringComparer.OrdinalIgnoreCase);
}
