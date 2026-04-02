using HotChocolate;

namespace Tyresoles.Web.GraphQL;

/// <summary>
/// Hot Chocolate replaces unhandled resolver exceptions with a generic "Unexpected Execution Error".
/// This filter copies the underlying exception message (NAV <see cref="System.ServiceModel.FaultException"/>, inner chain, etc.)
/// so clients see the same text as the WCF connector, without relying on resolver-local try/catch alone.
/// </summary>
public sealed class NavConnectorErrorFilter : IErrorFilter
{
    public IError OnError(IError error)
    {
        if (error.Exception is not { } ex)
            return error;

        if (!IsGenericExecutionMessage(error.Message))
            return error;

        var msg = NavConnectorErrorFormatting.FormatMessage(ex);
        if (string.IsNullOrWhiteSpace(msg))
            return error;

        return ErrorBuilder.FromError(error).SetMessage(msg).Build();
    }

    private static bool IsGenericExecutionMessage(string? message) =>
        string.Equals(message, "Unexpected Execution Error", StringComparison.Ordinal)
        || string.Equals(message, "Unexpected Execution Error.", StringComparison.Ordinal);
}
