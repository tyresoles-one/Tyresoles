namespace Tyresoles.Data.Features.NavisionEdits.Entities;

/// <summary>
/// A user's request to edit a Navision record. Body is stored as JSON.
/// Status flow: Draft → Pending → (PendingApproval) → Approved → Processed / Rejected.
/// </summary>
public class NavEditRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>FK to NavEditRequestType.</summary>
    public int RequestTypeId { get; set; }

    /// <summary>The primary key value of the record being edited (e.g. customer no "C0001").</summary>
    public string RecordKey { get; set; } = string.Empty;

    /// <summary>
    /// JSON body containing the requested changes.
    /// Structure: { "changes": [ { "column": "Name", "oldValue": "Old Name", "newValue": "New Name" } ] }
    /// </summary>
    public string RequestBody { get; set; } = "{}";

    /// <summary>User ID of the requester.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Display name of the requester (denormalized for easy display).</summary>
    public string? UserFullName { get; set; }

    /// <summary>Current status: Draft, Pending, PendingApproval, Approved, Processed, Rejected.</summary>
    public NavEditStatus Status { get; set; } = NavEditStatus.Pending;

    /// <summary>Optional remark by the requester.</summary>
    public string? Remark { get; set; }

    /// <summary>Admin remark when processing/rejecting.</summary>
    public string? AdminRemark { get; set; }

    /// <summary>User ID of the admin who processed the request.</summary>
    public string? ProcessedBy { get; set; }

    /// <summary>When the request was processed.</summary>
    public DateTime? ProcessedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public NavEditRequestType? RequestType { get; set; }
    public List<NavEditApproval> Approvals { get; set; } = new();
}

public enum NavEditStatus
{
    Draft = 0,
    Pending = 1,
    PendingApproval = 2,
    Approved = 3,
    Processed = 4,
    Rejected = 5
}
