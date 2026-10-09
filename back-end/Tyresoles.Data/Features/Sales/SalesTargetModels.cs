namespace Tyresoles.Data.Features.Sales;

/// <summary>
/// DTO representing a sales team with its calculated current month sales and next month target.
/// </summary>
public sealed class TeamSalesTargetDto
{
    public string TeamCode { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public string RespCenter { get; set; } = string.Empty;
    public decimal CurrentSale { get; set; }
    public decimal TargetMultiplier { get; set; }
    public decimal NextTarget { get; set; }
    public decimal ExistingTarget { get; set; }
    public DateTime? ExistingTargetEndDate { get; set; }
    public DateTime NextTargetEndDate { get; set; }
}

/// <summary>
/// Request parameters to preview calculated team sales targets.
/// </summary>
public sealed class PreviewTeamSalesTargetsRequest
{
    /// <summary>Responsibility center code (e.g. "BEL") or null/"ALL" for all centers.</summary>
    public string? RespCenter { get; set; }

    /// <summary>From date (ISO yyyy-MM-dd or parseable date string). Defaults to 1st day of current month.</summary>
    public string? FromDate { get; set; }

    /// <summary>To date (ISO yyyy-MM-dd or parseable date string). Defaults to current date.</summary>
    public string? ToDate { get; set; }

    /// <summary>Target end date for next month. Defaults to last day of next month.</summary>
    public string? TargetEndDate { get; set; }

    /// <summary>Sale type: "retread-ecomile" (default), "trade-other", or "all".</summary>
    public string? SaleType { get; set; } = "retread-ecomile";

    /// <summary>Rounding step for proposed target (e.g. 50000 for 0.5 Lakh, 25000 for 0.25L, -1 for Smart Adaptive, 0 for exact). Defaults to 50000.</summary>
    public decimal? RoundingStep { get; set; } = 50000m;
}

/// <summary>
/// Preview result containing calculated team sales and proposed next month targets.
/// </summary>
public sealed class TeamSalesTargetsPreviewResult
{
    public bool Success { get; set; } = true;
    public string Message { get; set; } = string.Empty;
    public string? RespCenter { get; set; }
    public decimal RespCenterMultiplier { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public DateTime NextTargetEndDate { get; set; }
    public decimal TotalCurrentSale { get; set; }
    public decimal TotalNextTarget { get; set; }
    public decimal RoundingStep { get; set; } = 50000m;
    public decimal EffectiveGrowthPercent { get; set; }
    public List<TeamSalesTargetDto> Items { get; set; } = new();
}

/// <summary>
/// Request parameters to commit generated targets to the database.
/// </summary>
public sealed class GenerateTeamSalesTargetsRequest
{
    public string? RespCenter { get; set; }
    public string? FromDate { get; set; }
    public string? ToDate { get; set; }
    public string? TargetEndDate { get; set; }
    public string? SaleType { get; set; } = "retread-ecomile";
    public decimal? RoundingStep { get; set; } = 50000m;
    public List<TeamSalesTargetOverrideInput>? TargetOverrides { get; set; }
}

/// <summary>
/// Optional manual override for a specific team target before committing.
/// </summary>
public sealed class TeamSalesTargetOverrideInput
{
    public string TeamCode { get; set; } = string.Empty;
    public decimal Target { get; set; }
}

/// <summary>
/// Result of generating and saving next month targets to the Team table.
/// </summary>
public sealed class GenerateTeamSalesTargetsResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int UpdatedCount { get; set; }
    public decimal TotalGeneratedTarget { get; set; }
    public decimal RoundingStep { get; set; } = 50000m;
    public decimal EffectiveGrowthPercent { get; set; }
    public List<TeamSalesTargetDto> Items { get; set; } = new();
}

/// <summary>
/// Responsibility Center with its Target Multiplier.
/// </summary>
public sealed class ResponsibilityCenterTargetMultiplierDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal TargetMultiplier { get; set; }
}
