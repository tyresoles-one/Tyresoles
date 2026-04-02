namespace Tyresoles.Easebuzz;

/// <summary>
/// Easebuzz API base URLs. Confirm from https://docs.easebuzz.in/docs/payment-gateway/ if endpoints change.
/// </summary>
internal static class EasebuzzUrls
{
    internal const string TestBase = "https://testpay.easebuzz.in/";
    internal const string ProdBase = "https://pay.easebuzz.in/";

    internal static string Base(bool sandbox) => sandbox ? TestBase : ProdBase;
}
