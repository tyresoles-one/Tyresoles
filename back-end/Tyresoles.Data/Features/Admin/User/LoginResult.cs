using Microsoft.Identity.Client;

namespace Tyresoles.Data.Features.Admin.User;

public sealed class LoginResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public LoginUser? User { get; init; }
    public List<Menu>? Menus { get; init; }
    public List<UserLocation>? Locations {  get; init; } 
    public string? Token { get; init; }
    /// <summary>When true, client must redirect to change-password. Use RequirePasswordChangeReason to choose form (Security PIN vs current password).</summary>
    public bool RequirePasswordChange { get; init; }
    /// <summary>FirstLogin = show Security PIN form; AdminReset or Expired = show current password form.</summary>
    public string? RequirePasswordChangeReason { get; init; }
}

public sealed class LoginUser
{
    public string UserId { get; init; } = string.Empty;
    public Guid UserSecurityId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string UserType { get; set; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityCode { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string RespCenter { get; set; } = string.Empty;
    public string UserSpecialToken {  get; set; } = string.Empty;
    public DateTime WorkDate { get; set; }
    public int Avatar { get; set; } = 0;
}

/// <summary>Top-level menu: one per distinct ParentMenu (PermissionSet.Parent Menu).</summary>
public sealed class Menu
{
    public string Label { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
    public List<SubMenu> SubMenus { get; init; } = new();
}

/// <summary>Second-level menu: one per distinct SubMenu within a ParentMenu.</summary>
public sealed class SubMenu
{
    public string Label { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
    public List<MenuItem> Items { get; init; } = new();
}

/// <summary>Leaf menu entry: one per PermissionSet row (Name, Action, Icon, Order).</summary>
public sealed class MenuItem
{
    public string Code{get; set;} = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string Options { get; init; } = string.Empty;
    public int Order { get; init; }
}

public sealed class UserLocation
{
    public string Code { get; set; } = String.Empty;
    public string Name { get; set; } = string.Empty;
    public byte Sale{ get; set; }
    public byte Purchase { get; set; }
    public byte Production { get; set; }
    public byte Payroll { get; set; }
    
}
