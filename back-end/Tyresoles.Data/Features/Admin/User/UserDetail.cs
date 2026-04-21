using System.Collections.Generic;

namespace Tyresoles.Data.Features.Admin.User;

public sealed class UserDetailRespCenterRow
{
    public string UserId { get; init; } = string.Empty;
    public string RespCenter { get; init; } = string.Empty;
    /// <summary>NAV <c>Default</c> flag (1 = default responsibility center for user).</summary>
    public int RespDefault { get; init; }
    public int Type { get; init; }
    public string Code { get; init; } = string.Empty;
}

public sealed class UserDetailPostingRow
{
    public string ResponsibilityCenter { get; init; } = string.Empty;
    /// <summary>yyyy-MM-dd for the admin date range picker.</summary>
    public string AllowPostingFrom { get; init; } = string.Empty;
    public string AllowPostingTo { get; init; } = string.Empty;
}

public sealed class UserDetailPermissionRow
{
    public string RoleId { get; init; } = string.Empty;
    public string RoleName { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
    public string AssignerName { get; init; } = string.Empty;
    /// <summary>NAV column spelling. yyyy-MM-dd when set.</summary>
    public string RoleExipryDate { get; init; } = string.Empty;
    public string Values { get; init; } = string.Empty;
    public int HomePath { get; init; }
}

public sealed class UserDetail
{
    public string UserId { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string UserType { get; init; } = string.Empty;
    public string MobileNo { get; init; } = string.Empty;
    public string AuthenticationEmail { get; init; } = string.Empty;
    public int State { get; init; }
    public int CanRunERP { get; init; }
    public int CanRunOldERP { get; init; }
    public int AllowAllMasters { get; init; }
    public decimal BackupStorageQuotaGB { get; init; }
    public string BackupAllowedFileTypes { get; init; } = string.Empty;
    public string BackupGDriveFolderID { get; init; } = string.Empty;
    public string? RDPPassword { get; init; }
    public string? NavConfigName { get; init; }
    /// <summary>SSL-VPN / FortiClient logon id from NAV User (Vpn UserID).</summary>
    public string? VpnUserId { get; init; }
    /// <summary>SSL-VPN password from NAV User (Vpn Password).</summary>
    public string? VpnPassword { get; init; }

    public IReadOnlyList<UserDetailRespCenterRow> RespCenterSetup { get; init; } = [];
    public IReadOnlyList<UserDetailPostingRow> PostingSetup { get; init; } = [];
    public IReadOnlyList<UserDetailPermissionRow> Permissions { get; init; } = [];
}
