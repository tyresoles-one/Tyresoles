namespace Tyresoles.Data.Features.NavisionEdits.Entities;

/// <summary>
/// Tracks individual approvals for a request that requires multi-level sign-off.
/// Each approval row corresponds to one level from the template's approvals array.
/// </summary>
public class NavEditApproval
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>FK to NavEditRequest.</summary>
    public Guid RequestId { get; set; }

    /// <summary>Approval level (1, 2, 3, …). Matches the "level" in FieldsJson approvals.</summary>
    public int Level { get; set; }

    /// <summary>Legacy role code (unused in userIds-based approvals).</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>Legacy label (unused in userIds-based approvals).</summary>
    public string? RoleLabel { get; set; }

    /// <summary>JSON array of Nav user IDs allowed to approve this level, snapshotted at submit.</summary>
    public string? ApproverUserIdsJson { get; set; }

    /// <summary>User ID of the approver (filled when they approve/reject).</summary>
    public string? ApprovedBy { get; set; }

    /// <summary>Approval status: Pending, Approved, Rejected.</summary>
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;

    /// <summary>Optional comment from the approver.</summary>
    public string? Comment { get; set; }

    public DateTime? ActionDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public NavEditRequest? Request { get; set; }
}

public enum ApprovalStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}
