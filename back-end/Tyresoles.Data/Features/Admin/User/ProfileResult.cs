using System;
using System.Collections.Generic;

namespace Tyresoles.Data.Features.Admin.User;

public sealed class ProfileResult
{
    public string UserId { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string UserType { get; init; } = string.Empty;
    public string MobileNo { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public int Avatar { get; init; }
    public DateTime LastPasswordChanged { get; init; }
    public int SecurityPIN { get; init; }
    public List<UserEntity> Entities { get; init; } = new();
}

public sealed class UserEntity
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
}

/// <summary>Input for updating user profile. Only non-null properties are applied.</summary>
public sealed class ProfileUpdateInput
{
    public string? FullName { get; init; }
    public string? MobileNo { get; init; }
    public string? Email { get; init; }
    public int? Avatar { get; init; }
    public int? SecurityPIN { get; init; }
}
