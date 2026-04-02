using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Tyresoles.Data;
using Tyresoles.Data.Features.Protean;
using Tyresoles.Protean;
using Tyresoles.Protean.Models.Gstin;
using Tyresoles.Protean.Services;
using Tyresoles.Protean.Session;
using Tyresoles.Data.Features.Sales;

namespace Tyresoles.Web.Controllers;

/// <summary>
/// Production API for Protean GSP (E-Invoice / E-Waybill) operations.
/// </summary>
[ApiController]
[Route("api/protean")]
[AllowAnonymous]
[Produces(MediaTypeNames.Application.Json)]
public sealed class ProteanController : ControllerBase
{
    private readonly IEInvoiceService _eInvoiceService;
    private readonly IEWaybillService _eWaybillService;
    private readonly IProteanSessionService _sessionService;
    private readonly IDataverseDataService _dataService;
    private readonly IProteanDataService _proteanDataService;
    private readonly IProteanService _proteanService;
    private readonly ILogger<ProteanController> _logger;

    private const string TenantKey = "NavLive";

    public ProteanController(
        IEInvoiceService eInvoiceService,
        IEWaybillService eWaybillService,
        IProteanSessionService sessionService,
        IDataverseDataService dataService,
        IProteanDataService proteanDataService,
        IProteanService proteanService,
        ILogger<ProteanController> logger)
    {
        _eInvoiceService = eInvoiceService ?? throw new ArgumentNullException(nameof(eInvoiceService));
        _eWaybillService = eWaybillService ?? throw new ArgumentNullException(nameof(eWaybillService));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        _proteanDataService = proteanDataService ?? throw new ArgumentNullException(nameof(proteanDataService));
        _proteanService = proteanService ?? throw new ArgumentNullException(nameof(proteanService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// [TEMPORARY] Test Protean auth token generation (E-Invoice session).
    /// Uses default credentials from config when gstin/username/password are not supplied.
    /// Remove or restrict this endpoint before production go-live.
    /// </summary>
    /// <param name="gstin">Optional. Defaults to Constants.DefaultGstin.</param>
    /// <param name="username">Optional. Defaults to Constants.DefaultEInvUsername.</param>
    /// <param name="password">Optional. Defaults to Constants.DefaultEInvPassword.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("test-auth")]
    [ProducesResponseType(typeof(ProteanTestAuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> TestAuth(
        [FromQuery] string? gstin,
        [FromQuery] string? username,
        [FromQuery] string? password,
        CancellationToken cancellationToken)
    {
        var g = gstin ?? Constants.DefaultGstin;
        var u = username ?? Constants.DefaultEInvUsername;
        var p = password ?? Constants.DefaultEInvPassword;

        if (string.IsNullOrWhiteSpace(g) || string.IsNullOrWhiteSpace(u) || string.IsNullOrWhiteSpace(p))
        {
            return BadRequest(new { error = "GSTIN, username and password are required (or use Constants defaults).", code = "MISSING_CREDENTIALS" });
        }

        try
        {
            var session = await _sessionService.GetEInvoiceSessionAsync(g, u, p, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Protean test-auth succeeded for GSTIN {Gstin}", g);

            return Ok(new ProteanTestAuthResponse
            {
                Api = "EInvoice",
                Gstin = g,
                Username = u,
                AuthToken = session.AuthToken,
                ExpiresAt = session.ExpiresAt,
                IsAlive = session.IsAlive,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Protean test-auth failed for GSTIN {Gstin}", g);
            return StatusCode(500, new { error = ex.Message, code = "PROTEAN_AUTH_FAILED" });
        }
    }

    /// <summary>
    /// Verify GSTIN by fetching master data from the IRP (same as Processor.GetGSTIN).
    /// Uses Protean default credentials for the master API.
    /// </summary>
    /// <param name="gstin">15-character GSTIN to verify (e.g. 29AAACT2744B1ZM).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>GSTIN details (trade name, legal name, address, state, status) or 404 if not found.</returns>
    [HttpGet("gstin/{gstin}")]
    [ProducesResponseType(typeof(GstinDetails), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetGstin(
        string gstin,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(gstin))
        {
            return BadRequest(new { error = "GSTIN is required.", code = "MISSING_GSTIN" });
        }

        var searchGstin = gstin.Trim();
        if (searchGstin.Length != 15)
        {
            return BadRequest(new { error = "GSTIN must be 15 characters.", code = "INVALID_GSTIN_LENGTH" });
        }

        try
        {
            var details = await _eInvoiceService.GetGstinAsync(searchGstin, cancellationToken).ConfigureAwait(false);
            if (details is null)
            {
                return NotFound(new { error = "GSTIN not found or no data returned from IRP.", gstin = searchGstin, code = "GSTIN_NOT_FOUND" });
            }

            return Ok(details);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetGSTIN failed for {Gstin}", searchGstin);
            return StatusCode(500, new { error = ex.Message, code = "PROTEAN_GET_GSTIN_FAILED" });
        }
    }

    /// <summary>
    /// Sync GSTIN from the IRP master (force refresh). Same as Processor.SyncGSTIN.
    /// </summary>
    /// <param name="gstin">15-character GSTIN to sync.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("gstin/{gstin}/sync")]
    [ProducesResponseType(typeof(GstinDetails), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SyncGstin(
        string gstin,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(gstin))
        {
            return BadRequest(new { error = "GSTIN is required.", code = "MISSING_GSTIN" });
        }

        var searchGstin = gstin.Trim();
        if (searchGstin.Length != 15)
        {
            return BadRequest(new { error = "GSTIN must be 15 characters.", code = "INVALID_GSTIN_LENGTH" });
        }

        try
        {
            var details = await _eInvoiceService.SyncGstinAsync(searchGstin, cancellationToken).ConfigureAwait(false);
            if (details is null)
            {
                return NotFound(new { error = "GSTIN not found or no data returned from IRP.", gstin = searchGstin, code = "GSTIN_NOT_FOUND" });
            }

            return Ok(details);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SyncGSTIN failed for {Gstin}", searchGstin);
            return StatusCode(500, new { error = ex.Message, code = "PROTEAN_SYNC_GSTIN_FAILED" });
        }
    }

    /// <summary>
    /// Fetches the name of a Customer, Vendor, or Transporter by their code.
    /// </summary>
    [HttpGet("party/{type}/{code}")]
    public async Task<IActionResult> GetPartyName(string type, string code, CancellationToken ct)
    {
        var scope = _dataService.ForTenant(TenantKey);
        var name = await _proteanDataService.GetPartyNameAsync(scope, type, code, ct).ConfigureAwait(false);
        if (name == null) return NotFound(new { error = "Party not found." });
        return Ok(new { name });
    }

    /// <summary>
    /// Updates the GST-related fields for a Customer or Vendor in the database.
    /// </summary>
    [HttpPost("gstin/save")]
    public async Task<IActionResult> UpdateGstinMaster([FromBody] UpdateGstinRequest request, CancellationToken ct)
    {
        var scope = _dataService.ForTenant(TenantKey);
        await _proteanDataService.UpdateGstinMasterAsync(
            scope,
            request.Type,
            request.Code,
            request.Gstin,
            request.TradeName,
            request.LegalName,
            request.Status,
            request.BlockStatus,
            ct).ConfigureAwait(false);

        return Ok(new { success = true });
    }

    /// <summary>
    /// Processes all pending E-Invoices for the NavLive tenant.
    /// Initiates IRN generation for each candidate in parallel for high throughput.
    /// </summary>
    [HttpPost("run-einv")]
    public async Task<IActionResult> RunEInvProcess(CancellationToken ct = default)
    {
        var scope = _dataService.ForTenant(TenantKey);
        var (processed, errors) = await _proteanService.RunEInvProcessAsync(scope, _eInvoiceService, ct).ConfigureAwait(false);

        return Ok(new { status = "COMPLETED", processed, errors });
    }

    /// <summary>
    /// Processes all pending E-Waybills for the NavLive tenant.
    /// Handles both Generate and Cancel requests based on current status.
    /// </summary>
    [HttpPost("run-ewb")]
    public async Task<IActionResult> RunEWBProcess(CancellationToken ct)
    {
        var scope = _dataService.ForTenant(TenantKey);
        
        var (processed, errors) = await _proteanService.RunEWBProcessAsync(scope, _eWaybillService, ct).ConfigureAwait(false);

        return Ok(new { status = "COMPLETED", processed, errors });
    }

    /// <summary>
    /// Loads e-invoice JSON from ERP, verifies it on the NIC portal using Puppeteer,
    /// and returns the resulting PDF as a base64 string.
    /// </summary>
    /// <param name="type">Document Type: "Invoice" or "CrNote".</param>
    /// <param name="no">Document No. in ERP.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("verify-einv")]
    public async Task<IActionResult> VerifyEInvoice([FromQuery] string type, [FromQuery] string no, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(no))
            return BadRequest(new { error = "Type and No are required.", code = "MISSING_PARAMS" });

        var scope = _dataService.ForTenant(TenantKey);
        try
        {
            var pdfPath = await _proteanService.VerifyEInvoiceAsync(scope, type, no, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(pdfPath) || !System.IO.File.Exists(pdfPath))
                return NotFound(new { error = "Verification failed to produce a PDF.", code = "VERIFY_FAILED" });

            var bytes = await System.IO.File.ReadAllBytesAsync(pdfPath, ct).ConfigureAwait(false);
            try { System.IO.File.Delete(pdfPath); } catch { /* best effort */ }

            return Ok(new { pdf = Convert.ToBase64String(bytes) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VerifyEInvoice failed for {Type} {No}", type, no);
            return StatusCode(500, new { error = ex.Message, code = "VERIFY_EXCEPTION" });
        }
    }
}

public sealed class UpdateGstinRequest
{
    public string Type { get; set; } = "";
    public string Code { get; set; } = "";
    public string Gstin { get; set; } = "";
    public string TradeName { get; set; } = "";
    public string LegalName { get; set; } = "";
    public string Status { get; set; } = "";
    public string BlockStatus { get; set; } = "";
}

/// <summary>Response for test-auth; does not expose SEK or password.</summary>
public sealed class ProteanTestAuthResponse
{
    public string Api { get; init; } = "";
    public string Gstin { get; init; } = "";
    public string Username { get; init; } = "";
    public string AuthToken { get; init; } = "";
    public DateTimeOffset ExpiresAt { get; init; }
    public bool IsAlive { get; init; }
}
