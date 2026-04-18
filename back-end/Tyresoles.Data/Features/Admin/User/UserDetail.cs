namespace Tyresoles.Data.Features.Admin.User;

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
}
