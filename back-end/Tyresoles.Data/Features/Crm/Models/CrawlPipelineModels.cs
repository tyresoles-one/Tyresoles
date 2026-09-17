using System;
using System.Collections.Generic;

namespace Tyresoles.Data.Features.Crm.Models;

public class CrawlQueueSummaryDto
{
    public int Total { get; set; }
    public int Pending { get; set; }
    public int Imported { get; set; }
    public int Duplicate { get; set; }
    public int Rejected { get; set; }
}

public class CrawlCheckpointDto
{
    public string TargetUrl { get; set; } = string.Empty;
    public int LastCrawledPage { get; set; }
    public int NextPageToCrawl { get; set; } = 1;
    public int TotalPagesCrawled { get; set; }
    public int TotalListingsDiscovered { get; set; }
    public int TotalPagesDetected { get; set; }
    public bool HasReachedEnd { get; set; }
    public string? UpdatedAt { get; set; }
    public CrawlQueueSummaryDto QueueSummary { get; set; } = new();
}

public class CrawlPipelineResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int PagesCrawled { get; set; }
    public int ListingsDiscovered { get; set; }
    public int NewLeadsEnqueued { get; set; }
    public int ProcessedCount { get; set; }
    public int ImportedCount { get; set; }
    public int DuplicateCount { get; set; }
    public int RejectedCount { get; set; }
    public int PendingRemaining { get; set; }
    public CrawlCheckpointDto? Checkpoint { get; set; }
}

public class StagedLeadItemDto
{
    public string Id { get; set; } = string.Empty;
    public string TargetUrl { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public int PageNum { get; set; }
    public string? CompanyName { get; set; }
    public string? ContactPerson { get; set; }
    public string? MobileNo { get; set; }
    public string? AltMobileNo { get; set; }
    public string? Email { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Location { get; set; }
    public string? Website { get; set; }
    public string Status { get; set; } = "PENDING";
    public string? ValidationError { get; set; }
    public string? CrmContactId { get; set; }
    public string? CreatedAt { get; set; }
    public string? ProcessedAt { get; set; }
}

public class BadgeMetadataDto
{
    public bool HasBadgeDetected { get; set; }
    public string? BadgeText { get; set; }
    public int? CurrentBadgePage { get; set; }
    public int? TotalBadgePages { get; set; }
    public string? NextBadgeUrl { get; set; }
    public int? TotalItemsCount { get; set; }
}

public class AutoExtractResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string TargetUrl { get; set; } = string.Empty;
    public int PagesCrawled { get; set; }
    public int ListingsDiscovered { get; set; }
    public int NewLeadsEnqueued { get; set; }
    public int ProcessedCount { get; set; }
    public int ImportedCount { get; set; }
    public int DuplicateCount { get; set; }
    public int RejectedCount { get; set; }
    public BadgeMetadataDto? BadgeMetadata { get; set; }
    public CrawlCheckpointDto? Checkpoint { get; set; }
    public List<StagedLeadItemDto> Leads { get; set; } = new();
}

