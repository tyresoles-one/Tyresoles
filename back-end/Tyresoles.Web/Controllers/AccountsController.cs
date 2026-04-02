using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tyresoles.Data.Features.Common;

namespace Tyresoles.Web.Controllers;

/// <summary>REST helpers for shared reference data used by accounts / procurement UIs.</summary>
[ApiController]
[Route("api/accounts")]
[Authorize]
[Produces(MediaTypeNames.Application.Json)]
public sealed class AccountsController : ControllerBase
{
    private readonly ICommonDataService _commonDataService;

    public AccountsController(ICommonDataService commonDataService)
    {
        _commonDataService = commonDataService;
    }

    /// <summary>
    /// Returns Indian states for dropdowns. Optional comma-separated codes filter the list.
    /// Response: { success, data } with code/name rows for the front-end apiFetch helper.
    /// </summary>
    [HttpPost("states")]
    public async Task<IActionResult> GetStates([FromBody] StatesRequest? body, CancellationToken cancellationToken)
    {
        var states = await _commonDataService.GetStateAsync(body?.Codes, cancellationToken).ConfigureAwait(false);
        var data = states.Select(s => new { code = s.Code, name = s.Description }).ToList();
        return Ok(new { success = true, data });
    }
}

public sealed class StatesRequest
{
    /// <summary>Optional comma-separated state codes to restrict results.</summary>
    public string? Codes { get; set; }
}
