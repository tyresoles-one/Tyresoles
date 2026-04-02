using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tyresoles.Data;
using Tyresoles.Data.Features.Sales;

namespace Tyresoles.Web.Controllers;

/// <summary>
/// Dealer document images: NAV <c>Images</c> table (SQL insert on upload; list reads same table).
/// </summary>
[ApiController]
[Route("api/dealers/{dealerCode}/documents")]
[Authorize]
[Produces(MediaTypeNames.Application.Json)]
public sealed class DealerDocumentsController : ControllerBase
{
    private const string TenantKey = "NavLive";
    private readonly IDataverseDataService _dataService;
    private readonly ISalesService _salesService;
    private readonly ILogger<DealerDocumentsController> _logger;

    public DealerDocumentsController(
        IDataverseDataService dataService,
        ISalesService salesService,
        ILogger<DealerDocumentsController> logger)
    {
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        _salesService = salesService ?? throw new ArgumentNullException(nameof(salesService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Lists stored images for the dealer document number and NAV document type.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(DealerDocumentsListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        string dealerCode,
        [FromQuery] int docType = 1,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dealerCode))
            return BadRequest(new { error = "Dealer code is required." });

        using var scope = _dataService.ForTenant(TenantKey);
        var items = await _salesService
            .GetDealerDocumentImagesAsync(scope, dealerCode.Trim(), docType, cancellationToken)
            .ConfigureAwait(false);
        return Ok(new DealerDocumentsListResponse { Items = items });
    }

    /// <summary>Uploads multiple images; line numbers are assigned on the server.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(UploadDealerDocumentsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Upload(
        string dealerCode,
        [FromBody] UploadDealerDocumentsRequest? body,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dealerCode))
            return BadRequest(new { error = "Dealer code is required." });
        if (body?.Images == null || body.Images.Count == 0)
            return BadRequest(new { error = "At least one image is required." });

        using var scope = _dataService.ForTenant(TenantKey);
        try
        {
            await _salesService
                .UploadDealerDocumentImagesAsync(scope, dealerCode.Trim(), body.DocType, body.Images, cancellationToken)
                .ConfigureAwait(false);
            _logger.LogInformation(
                "Uploaded {Count} dealer document image(s) for {DealerCode}, docType {DocType}",
                body.Images.Count,
                dealerCode,
                body.DocType);
            return Ok(new UploadDealerDocumentsResponse { Success = true, Message = "Images saved." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload dealer documents failed for {DealerCode}", dealerCode);
            return StatusCode(500, new { error = ex.InnerException?.Message ?? ex.Message });
        }
    }
}

public sealed class DealerDocumentsListResponse
{
    public IReadOnlyList<DealerDocumentImageDto> Items { get; init; } = Array.Empty<DealerDocumentImageDto>();
}

public sealed class UploadDealerDocumentsRequest
{
    /// <summary>NAV <c>Doc_ Type</c> (default 1 — align with legacy client).</summary>
    public int DocType { get; set; } = 1;

    /// <summary>Base64 strings (optionally with <c>data:image/...;base64,</c> prefix).</summary>
    public List<string> Images { get; set; } = new();
}

public sealed class UploadDealerDocumentsResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
}
