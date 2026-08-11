using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tyresoles.Data.Features.Common;
using Tyresoles.Data.Features.Merger;
using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Web.Controllers;

[ApiController]
[Route("api/merger")]
[Authorize]
[Produces(MediaTypeNames.Application.Json)]
public sealed class MergerController : ControllerBase
{
    private readonly IMergerService _mergerService;
    private readonly IDataverse _dataverse;

    public MergerController(IMergerService mergerService, IDataverse dataverse)
    {
        _mergerService = mergerService ?? throw new ArgumentNullException(nameof(mergerService));
        _dataverse = dataverse ?? throw new ArgumentNullException(nameof(dataverse));
    }

    [HttpGet("oldcompanies")]
    public IActionResult OldCompanies([FromQuery] string business = "")
    {
        var data = _mergerService.GetOldCompanies(business);
        return Ok(new { success = true, data });
    }

    [HttpGet("oldrespcenters")]
    public async Task<IActionResult> OldRespCenters([FromQuery] string company = "", CancellationToken ct = default)
    {
        using var scope = _dataverse.ForTenant("NavLive");
        var data = await _mergerService.GetOldRespCentersAsync(scope, company, ct);
        return Ok(new { success = true, data });
    }

    [HttpPost("getmapper")]
    public async Task<IActionResult> GetMapper([FromBody] EntityMapper entity, CancellationToken ct = default)
    {
        using var scope = _dataverse.ForTenant("NavLive");
        using var oldScope = _dataverse.ForTenant("NavOld");
        var data = await _mergerService.GetMergerEntityAsync(scope, oldScope, entity, ct);
        return Ok(new { success = true, data });
    }

    [HttpPost("prepareentity")]
    public async Task<IActionResult> PrepareEntity([FromBody] EntityMapper entity, CancellationToken ct = default)
    {
        using var scope = _dataverse.ForTenant("NavLive");
        using var oldScope = _dataverse.ForTenant("NavOld");
        var data = await _mergerService.PrepareEntityAsync(scope, oldScope, entity, ct);
        return Ok(new { success = true, data });
    }

    [HttpPost("getoldinvlines")]
    public async Task<IActionResult> GetOldInvLines([FromBody] InvoiceMapper invLine, CancellationToken ct = default)
    {
        using var scope = _dataverse.ForTenant("NavOld");
        var data = await _mergerService.GetOldInvLinesAsync(scope, invLine, ct);
        return Ok(new { success = true, data });
    }

    [HttpPost("createclaimonoldinv")]
    public async Task<IActionResult> CreateClaimOnOldInv([FromBody] InvLineMapper invLine, CancellationToken ct = default)
    {
        using var scope = _dataverse.ForTenant("NavLive");
        using var oldScope = _dataverse.ForTenant("NavOld");
        var data = await _mergerService.CreateClaimOnOldInvAsync(scope, oldScope, invLine, ct);
        return Ok(new { success = true, data });
    }
}
