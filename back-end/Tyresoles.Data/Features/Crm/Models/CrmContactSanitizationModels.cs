using System;
using System.Collections.Generic;

namespace Tyresoles.Data.Features.Crm.Models;

public class CrmContactSanitizationStatsDto
{
    public int TotalContacts { get; set; }
    public int UnalignedStateCount { get; set; }
    public int AlignedStateCount { get; set; }
    public int TyresolesTagCount { get; set; }
    public int CleanTagsCount { get; set; }
    public int TotalWebLinkCount { get; set; }
    public int PendingWebEnrichmentCount { get; set; }
    public int EnrichedWebCount { get; set; }
    public List<CrmContactSampleDto> RecentEnrichedSamples { get; set; } = new();
}

public class CrmContactSampleDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? State { get; set; }
    public string? Tags { get; set; }
    public string? SourceUrl { get; set; }
    public DateTime? ModifiedAt { get; set; }
}

public class CrmBatchEnrichmentResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int ProcessedCount { get; set; }
    public int ScrapedSuccessCount { get; set; }
    public int ScrapedFailedCount { get; set; }
    public int TotalFeaturesAdded { get; set; }
    public int RemainingPendingCount { get; set; }
    public List<CrmEnrichmentItemDto> ProcessedItems { get; set; } = new();
}

public class CrmEnrichmentItemDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? State { get; set; }
    public string? OldTags { get; set; }
    public string? NewTags { get; set; }
    public string? SourceUrl { get; set; }
    public List<string> FeaturesFound { get; set; } = new();
    public bool Success { get; set; }
    public string? Error { get; set; }
}
