namespace Tyresoles.Data.Features.Sales;

/// <summary>Row from NAV <c>Images</c> for a dealer document (SOAP + SQL read).</summary>
public sealed class DealerDocumentImageDto
{
    public int LineNo { get; init; }
    /// <summary>Raw base64 (no data-URL prefix); empty if blob missing.</summary>
    public string ImageBase64 { get; init; } = "";
}
