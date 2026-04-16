using Tyresoles.Data.Features.NavisionEdits.Entities;

namespace Tyresoles.Data.Features.NavisionEdits;

public interface INavEditService
{
    // ── Request Types (Admin CRUD) ──────────────────────────────
    Task<IReadOnlyList<NavEditRequestType>> GetRequestTypesAsync(bool activeOnly = true, CancellationToken ct = default);
    Task<NavEditRequestType?> GetRequestTypeByIdAsync(int id, CancellationToken ct = default);
    Task<NavEditRequestType> SaveRequestTypeAsync(NavEditRequestTypeInput input, string userId, CancellationToken ct = default);
    Task<bool> DeleteRequestTypeAsync(int id, CancellationToken ct = default);

    // ── Record Lookup (dynamic) ─────────────────────────────────
    Task<List<Dictionary<string, object?>>> LookupRecordsAsync(int requestTypeId, string? search, int take = 20, CancellationToken ct = default);
    Task<Dictionary<string, object?>?> GetRecordByKeyAsync(int requestTypeId, string recordKey, CancellationToken ct = default);

    /// <summary>Nav Live base table names (INFORMATION_SCHEMA), for template designer.</summary>
    Task<IReadOnlyList<string>> GetNavLiveTableNamesAsync(CancellationToken ct = default);

    /// <summary>Column names for a Nav Live table (INFORMATION_SCHEMA).</summary>
    Task<IReadOnlyList<string>> GetNavLiveColumnNamesForTableAsync(string tableName, CancellationToken ct = default);

    // ── Requests (User + Admin) ─────────────────────────────────
    Task<NavEditRequest> SubmitRequestAsync(NavEditRequestInput input, string userId, string? userFullName, CancellationToken ct = default);
    Task<IReadOnlyList<NavEditRequestDto>> GetMyRequestsAsync(string userId, CancellationToken ct = default);
    Task<IReadOnlyList<NavEditRequestDto>> GetAllRequestsAsync(NavEditStatus? statusFilter = null, CancellationToken ct = default);
    Task<IReadOnlyList<NavEditRequestDto>> GetPendingApprovalsAsync(string userId, CancellationToken ct = default);
    Task<NavEditRequestDto?> GetRequestByIdAsync(Guid requestId, CancellationToken ct = default);

    // ── Actions ─────────────────────────────────────────────────
    Task<bool> ApproveRequestAsync(Guid requestId, int level, string userId, string? comment, CancellationToken ct = default);
    Task<bool> RejectRequestAsync(Guid requestId, string userId, string? comment, bool isApproval = false, int level = 0, CancellationToken ct = default);
    Task<bool> ProcessRequestAsync(Guid requestId, string adminUserId, string? adminRemark, CancellationToken ct = default);

    /// <summary>Re-send IT admin + current-level approver notifications (requester only; pending workflows).</summary>
    Task<bool> ResendSubmitNotificationsAsync(Guid requestId, string userId, CancellationToken ct = default);
}

// ── DTOs & Inputs ───────────────────────────────────────────────

public class NavEditRequestTypeInput
{
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public string NavTable { get; set; } = string.Empty;
    public string NavPrimaryKeyColumn { get; set; } = string.Empty;
    public string FieldsJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public class NavEditRequestInput
{
    public int RequestTypeId { get; set; }
    public string RecordKey { get; set; } = string.Empty;
    public string RequestBody { get; set; } = "{}";
    public string? Remark { get; set; }
}

public class NavEditRequestDto
{
    public Guid Id { get; set; }
    public int RequestTypeId { get; set; }
    public string RequestTypeName { get; set; } = string.Empty;
    public string RequestTypeCode { get; set; } = string.Empty;
    public string? RequestTypeIcon { get; set; }
    public string RecordKey { get; set; } = string.Empty;
    public string RequestBody { get; set; } = "{}";
    public string UserId { get; set; } = string.Empty;
    public string? UserFullName { get; set; }
    public int Status { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string? Remark { get; set; }
    public string? AdminRemark { get; set; }
    public string? ProcessedBy { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<NavEditApprovalDto> Approvals { get; set; } = new();
}

public class NavEditApprovalDto
{
    public Guid Id { get; set; }
    public int Level { get; set; }
    /// <summary>Legacy; prefer <see cref="ApproverUserIds"/>.</summary>
    public string Role { get; set; } = string.Empty;
    /// <summary>Legacy; prefer <see cref="ApproverUserIds"/>.</summary>
    public string? RoleLabel { get; set; }
    /// <summary>Nav user IDs allowed to approve this level (from snapshot at submit).</summary>
    public List<string> ApproverUserIds { get; set; } = new();
    public string? ApprovedBy { get; set; }
    public int Status { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public DateTime? ActionDate { get; set; }
}
