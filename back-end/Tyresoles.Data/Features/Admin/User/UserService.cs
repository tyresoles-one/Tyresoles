using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq.Expressions;
using Dataverse.NavLive;
using Tyresoles.Data.Features.Admin.Auth;
using Tyresoles.Data.Features.Admin.Session;
using Tyresoles.Sql.Abstractions;
namespace Tyresoles.Data.Features.Admin.User;

public sealed class UserService : IUserService
{
    private const string PlatformWeb = "web";
    private const string PlatformWin = "win";
    private const string UserTypeDealer = "DEALER";
    private const string UserTypeSales = "SALES";
    private const string DefaultPassword = "TyrePass@1";

    /// <summary>Nav blank date (1/1/1573) - treat as "no date" for first login.</summary>
    private static readonly DateTime NavBlankDate = new(1573, 1, 1);

    private readonly IDataverseDataService _dataService;
    private readonly IPasswordEncryptionService _passwordEncryption;
    private readonly ISessionStore _sessionStore;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly JwtExpiryOptions _jwtExpiryOptions;
    private readonly PasswordPolicyOptions _passwordPolicy;

    public UserService(
        IDataverseDataService dataService,
        IPasswordEncryptionService passwordEncryption,
        ISessionStore sessionStore,
        IJwtTokenService jwtTokenService,
        JwtExpiryOptions jwtExpiryOptions,
        PasswordPolicyOptions passwordPolicy)
    {
        _dataService = dataService;
        _passwordEncryption = passwordEncryption;
        _sessionStore = sessionStore;
        _jwtTokenService = jwtTokenService;
        _jwtExpiryOptions = jwtExpiryOptions;
        _passwordPolicy = passwordPolicy;
    }

    /// <summary>Normalize Nav "Password Changed Date". Returns null if no value or known blank sentinel (0D: 1/1/1573, 1/1/1753, 1/1/1900, 1/1/0001).</summary>
    private static DateTime? ToPasswordChangedDate(DateTime? value)
    {
        if (!value.HasValue) return null;
        var d = value.Value;
        if (d.Month != 1 || d.Day != 1) return d;
        if (d.Year == 1573 || d.Year == 1753 || d.Year == 1900 || d.Year <= 1) return null;
        return d;
    }

    private static bool MatchesPlatform(string? platform, PermissionSet p)
    {
        if (platform.IsEmpty() || (platform != PlatformWeb && platform != PlatformWin))
            return true;
        if (platform == PlatformWeb) return p.WebApp == 1;
        return p.ERPApp == 1;
    }

    private static List<Menu> BuildMenusFromPermissions(IEnumerable<PermissionSet> permissions, IReadOnlyList<AccessControl> accessControls)
    {
        var valuesByRoleId = accessControls.Where(a => a.RoleID.HasValue()).ToDictionary(a => a.RoleID!, StringComparer.OrdinalIgnoreCase);
        return permissions
            .GroupBy(p => p.ParentMenu ?? "")
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(parentGroup => new Menu
            {
                Label = parentGroup.Key,
                Icon = parentGroup.First().ParentMenuIcon ?? "",
                SubMenus = parentGroup
                    .GroupBy(p => p.SubMenu ?? "")
                    .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(subGroup => new SubMenu
                    {
                        Label = subGroup.Key,
                        Icon = subGroup.First().SubMenuIcon ?? "",
                        Items = subGroup
                            .OrderBy(p => p.Order)
                            .ThenBy(p => p.Name ?? "", StringComparer.OrdinalIgnoreCase)
                            .Select(p => new MenuItem
                            {
                                Code = p.RoleID ?? "",
                                Label = p.Name ?? "",
                                Icon = p.Icon ?? "",
                                Action = p.Action ?? "",
                                Options = valuesByRoleId.TryGetValue(p.RoleID ?? "", out var values) ? (values?.Values ?? "") : "",
                                Order = p.Order
                            })
                            .ToList()
                    })
                    .ToList()
            })
            .ToList();
    }

    private static DateTime GetEffectiveWorkDate(ResponsibilityCenter? defCenter, ResponsibilityCenter? secondaryCenter)
    {
        var workDate = DateTime.Now;
        var closureDate = secondaryCenter?.SalesClosureEndDate ?? defCenter?.SalesClosureEndDate;
        if (!closureDate.HasValue) return workDate;
        var nextMonth = closureDate.Value.AddMonths(1);
        var nextMonthEndDate = new DateTime(nextMonth.Year, nextMonth.Month, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));
        return workDate.Date > nextMonthEndDate ? nextMonthEndDate : workDate;
    }

    private static UserLoginGraph BuildUserGraph(UserLoginRow firstRow)
    {
        return new UserLoginGraph
        {
            User = firstRow.User,
            Setup = firstRow.Setup,
            DefCenter = firstRow.DefCenter,
            SecondaryCenter = firstRow.SecondaryCenter,
            Permissions = new(),
            AccessControls = new()
        };
    }

    private async Task<(string FullName, string Title, string Department, string? UserSpecialToken)> ResolveDisplayInfoAsync(
        ITenantScope scope,
        RespCenterUserSetup defLocation,
        string defaultFullName,
        CancellationToken cancellationToken)
    {
        string fullName = defaultFullName ?? "";
        string title = "";
        string department = "";
        string userSpecialToken = "";

        if (defLocation.Type == RespCenterUserSetupType.Employee && defLocation.Code.HasValue())
        {
            var emp = await scope.Query<Employee>()
                .Where(e => e.No == defLocation.Code)
                .FirstOrDefaultAsync(cancellationToken);
            if (emp != null)
            {
                fullName = emp.Initials ?? "";
                userSpecialToken = emp.SpecialToken ?? "";
                title = emp.JobTitle.HasValue() ? emp.JobTitle : (emp.Title ?? "");
                if (emp.Department.HasValue())
                {
                    var groupCategory = await scope.Query<GroupCategory>()
                        .Where(g => g.Code == emp.Department)
                        .FirstOrDefaultAsync(cancellationToken);
                    department = groupCategory?.Name ?? emp.Department;
                }
                else
                {
                    department = "";
                }
            }
        }
        else if ((defLocation.Type == RespCenterUserSetupType.Partner || defLocation.Type == RespCenterUserSetupType.PartnerGroup) && defLocation.Code.HasValue())
        {
            var sp = await scope.Query<SalespersonPurchaser>()
                .Where(s => s.Code == defLocation.Code)
                .FirstOrDefaultAsync(cancellationToken);
            if (sp != null && sp.Name.HasValue())
                fullName = sp.Name;
        }

        return (fullName, title, department, userSpecialToken);
    }

    /// <summary>Named DTO for the login query projection. Used instead of an anonymous type so the SQL materializer can instantiate it (requires a parameterless constructor).</summary>
    private class UserLoginRow
    {
        public Dataverse.NavLive.User User { get; set; } = null!;
        public RespCenterUserSetup Setup { get; set; } = null!;
        public ResponsibilityCenter DefCenter { get; set; } = null!;
        public ResponsibilityCenter SecondaryCenter { get; set; } = null!;        
        public PermissionSet Permission { get; set; } = null!;
        public AccessControl AccessControl { get; set; } = null!;
    }

    private class UserLoginGraph
    {
        public Dataverse.NavLive.User User { get; set; } = null!;
        public RespCenterUserSetup Setup { get; set; } = null!;
        public ResponsibilityCenter DefCenter { get; set; } = null!;
        public ResponsibilityCenter SecondaryCenter { get; set; } = null!;        
        public List<PermissionSet> Permissions { get; set; } = new();
        public List<AccessControl> AccessControls { get; set; } = new();
        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }

    public async Task<LoginResult> LoginAsync(string username, string password, string? platform = null, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default)
    {
        if (username.IsEmpty())
            return new LoginResult { Success = false, Message = "Username is required.", User = null };

        using var scope = _dataService.ForNavLive();

        var userGraphRaw = await LoadUserBaseGraphAsync(scope, username, cancellationToken);
        var firstRow = userGraphRaw.FirstOrDefault();
        if (firstRow == null)
            return new LoginResult { Success = false, Message = "Invalid username.", User = null };

        var user = firstRow.User;
        if (user == null)
            return new LoginResult { Success = false, Message = "Invalid username.", User = null };
                
        bool isValidPassword = false;

        if ((password.Length <= 4))
        {
            isValidPassword = int.TryParse(password, out var pin) && user.SecurityPin == pin;
            Console.WriteLine($"pin: {pin}, user : {user.SecurityPin}");
        }            
        else
            isValidPassword = _passwordEncryption.IsPasswordMatch(password, user.Salt, user.EncryptedPassword);
        
        if (!isValidPassword )
            return new LoginResult { Success = false, Message = "Invalid username or password.", User = null };

        // Defer loading permissions and roles until after successful password validation
        var accessControls = (await scope.Query<AccessControl>()
            .Where(ac => ac.UserSecurityID == user.UserSecurityID)
            .ToArrayAsync(cancellationToken))
            .ToList();

        var roleIds = accessControls.Select(ac => ac.RoleID).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        var permissions = new List<PermissionSet>();
        if (roleIds.Count > 0)
        {
            // Load permissions for all assigned roles
            permissions = (await scope.Query<PermissionSet>()
                .Where(ps => roleIds.Contains(ps.RoleID))
                .ToArrayAsync(cancellationToken))
                .Where(p => MatchesPlatform(platform, p))
                .ToList();
        }

        var userGraph = BuildUserGraph(firstRow);
        userGraph.AccessControls = accessControls;
        userGraph.Permissions = permissions;

        var defLocation = userGraph.Setup;
        if (defLocation == null)
            return new LoginResult { Success = false, Message = "User has no default configuration.", User = null };

        var passwordChangedDate = ToPasswordChangedDate(user.PasswordChangedDate);
        var requirePasswordChange = false;
        string? requirePasswordChangeReason = null;
        if (!passwordChangedDate.HasValue)
        {
            requirePasswordChange = true;
            requirePasswordChangeReason = "FirstLogin";
        }
        else if (user.ChangePassword == 1)
        {
            requirePasswordChange = true;
            requirePasswordChangeReason = "AdminReset";
        }
        else if (_passwordPolicy.ExpiryDays > 0 && (DateTime.UtcNow - passwordChangedDate.Value).TotalDays > _passwordPolicy.ExpiryDays)
        {
            requirePasswordChange = true;
            requirePasswordChangeReason = "Expired";
        }
     
        var workDate = GetEffectiveWorkDate(userGraph.DefCenter, userGraph.SecondaryCenter);
        var (resolvedFullName, resolvedTitle, resolvedDepartment, resolvedUserSpecialToken) = await ResolveDisplayInfoAsync(scope, defLocation, user.FullName, cancellationToken);

        var secondaryRespCenter = userGraph.SecondaryCenter;
        var defRespCenterCode = userGraph.DefCenter?.Code;

        if (userGraph.AccessControls.Count == 0 && userGraph.Permissions.Count > 0)
        {
            var loadedAc = await scope.Query<AccessControl>()
                .Where(ac => ac.UserSecurityID == user.UserSecurityID)
                .ToArrayAsync(cancellationToken);
            if (loadedAc.Length > 0)
            {
                userGraph.AccessControls.Clear();
                userGraph.AccessControls.AddRange(loadedAc);
            }
        }

        var menus = BuildMenusFromPermissions(userGraph.Permissions, userGraph.AccessControls);

        var qry = scope.Query<RespCenterUserSetup>().Where(c => c.UserID == user.UserName).Select(c => new { c.RespCenter });
        var respCenters = await scope.Query<ResponsibilityCenter>()
            .Where(c=>c.Code, qry, SubqueryOperator.In)
            .ToArrayAsync(cancellationToken);
        List<UserLocation> locations = new List<UserLocation>();
        foreach (var location in respCenters)
        {
            locations.Add(new UserLocation
            {
                Code = location.Code,
                Name = location.Name,
                Payroll = location.Payroll,
                Production = location.Production,
                Purchase = location.Purchase,
                Sale = location.Sale,
            });
        }
        var userType = user.UserType ?? string.Empty;
        var entityType = defLocation.Type.ToString();
        var entityCode = defLocation.Code ?? string.Empty;

        var expiresInHours = IsDealerOrSales(userType)
            ? _jwtExpiryOptions.DealerSalesExpiryHours
            : _jwtExpiryOptions.DefaultExpiryHours;
        var expiresIn = TimeSpan.FromHours(expiresInHours);
        var createdAtUtc = DateTime.UtcNow;
        var expiresAtUtc = createdAtUtc.Add(expiresIn);

        var loginUserId = user.UserName ?? string.Empty;
        // Do not remove other sessions for multi-device support
        // if (!string.IsNullOrEmpty(loginUserId))
        //     await _sessionStore.RemoveByUserAsync(loginUserId, cancellationToken);

        var refreshTokenExpiresIn = TimeSpan.FromDays(30); // 30 days refresh token validity
        var refreshTokenExpiresAtUtc = createdAtUtc.Add(refreshTokenExpiresIn);
        var refreshToken = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));

        var sessionId = Guid.NewGuid().ToString("N");

        var session = new SessionInfo
        {
            SessionId = sessionId,
            UserId = loginUserId,
            UserSecurityId = user.UserSecurityID,
            UserType = userType,
            EntityType = entityType,
            EntityCode = entityCode,
            Department = resolvedDepartment,
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = refreshTokenExpiresAtUtc, // Session lives as long as the refresh token
            RefreshToken = refreshToken,
            RefreshTokenExpiresAtUtc = refreshTokenExpiresAtUtc,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };
        await _sessionStore.CreateAsync(session, cancellationToken);

        var token = _jwtTokenService.GenerateToken(new JwtTokenRequest
        {
            UserId = session.UserId,
            UserSecurityId = session.UserSecurityId,
            UserType = session.UserType,
            EntityType = session.EntityType,
            EntityCode = session.EntityCode,
            Department = session.Department,            
            SessionId = sessionId,
            ExpiresIn = expiresIn
        });

        return new LoginResult
        {
            Success = true,
            User = new LoginUser
            {
                UserId = user.UserName ?? string.Empty,
                UserSecurityId = user.UserSecurityID,
                FullName = resolvedFullName,
                UserType = userType,
                Title = resolvedTitle,
                UserSpecialToken = resolvedUserSpecialToken,
                EntityType = entityType,
                EntityCode = entityCode,
                Department = resolvedDepartment,
                RespCenter = secondaryRespCenter?.Code ?? defRespCenterCode ?? defLocation.RespCenter ?? string.Empty,
                WorkDate = workDate,
                Avatar = user.Avatar
            },
            Menus = menus,
            Token = token,
            RefreshToken = refreshToken,
            Locations = locations,
            RequirePasswordChange = requirePasswordChange,
            RequirePasswordChangeReason = requirePasswordChangeReason
        };
    }

    public async Task<LoginResult> RefreshTokenAsync(string token, string refreshToken, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(refreshToken))
            return new LoginResult { Success = false, Message = "Invalid tokens." };

        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token))
            return new LoginResult { Success = false, Message = "Invalid access token format." };

        var jwtToken = handler.ReadJwtToken(token);
        var sessionIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "sessionId");
        if (sessionIdClaim == null)
            return new LoginResult { Success = false, Message = "Invalid access token." };

        var sessionId = sessionIdClaim.Value;
        var session = await _sessionStore.GetAsync(sessionId, cancellationToken);
        
        if (session == null || session.RefreshToken != refreshToken || session.RefreshTokenExpiresAtUtc <= DateTime.UtcNow)
        {
            if (session != null)
                await _sessionStore.RemoveAsync(sessionId, cancellationToken);
            return new LoginResult { Success = false, Message = "Session expired or invalid refresh token." };
        }

        using var scope = _dataService.ForNavLive();
        var userGraphRaw = await LoadUserBaseGraphAsync(scope, session.UserId, cancellationToken);
        var firstRow = userGraphRaw.FirstOrDefault();
        if (firstRow == null || firstRow.User == null)
        {
            await _sessionStore.RemoveAsync(sessionId, cancellationToken);
            return new LoginResult { Success = false, Message = "User not found." };
        }

        var user = firstRow.User;
        var accessControls = (await scope.Query<AccessControl>()
            .Where(ac => ac.UserSecurityID == user.UserSecurityID)
            .ToArrayAsync(cancellationToken)).ToList();

        var roleIds = accessControls.Select(ac => ac.RoleID).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        var permissions = new List<PermissionSet>();
        if (roleIds.Count > 0)
        {
            permissions = (await scope.Query<PermissionSet>()
                .Where(ps => roleIds.Contains(ps.RoleID))
                .ToArrayAsync(cancellationToken)).ToList(); // Platform won't be filtered strictly on refresh since we don't have it explicitly stored unless we use UserAgent
        }

        var userGraph = BuildUserGraph(firstRow);
        userGraph.AccessControls = accessControls;
        userGraph.Permissions = permissions;

        var defLocation = userGraph.Setup;
        var workDate = GetEffectiveWorkDate(userGraph.DefCenter, userGraph.SecondaryCenter);
        var (resolvedFullName, resolvedTitle, resolvedDepartment, resolvedUserSpecialToken) = await ResolveDisplayInfoAsync(scope, defLocation, user.FullName, cancellationToken);

        var menus = BuildMenusFromPermissions(userGraph.Permissions, userGraph.AccessControls);

        var qry = scope.Query<RespCenterUserSetup>().Where(c => c.UserID == user.UserName).Select(c => new { c.RespCenter });
        var respCenters = await scope.Query<ResponsibilityCenter>()
            .Where(c=>c.Code, qry, SubqueryOperator.In)
            .ToArrayAsync(cancellationToken);
        List<UserLocation> locations = new List<UserLocation>();
        foreach (var location in respCenters)
        {
            locations.Add(new UserLocation
            {
                Code = location.Code,
                Name = location.Name,
                Payroll = location.Payroll,
                Production = location.Production,
                Purchase = location.Purchase,
                Sale = location.Sale,
            });
        }

        var expiresInHours = IsDealerOrSales(user.UserType ?? "") ? _jwtExpiryOptions.DealerSalesExpiryHours : _jwtExpiryOptions.DefaultExpiryHours;
        var expiresIn = TimeSpan.FromHours(expiresInHours);
        
        var newAccessToken = _jwtTokenService.GenerateToken(new JwtTokenRequest
        {
            UserId = session.UserId,
            UserSecurityId = session.UserSecurityId,
            UserType = session.UserType,
            EntityType = session.EntityType,
            EntityCode = session.EntityCode,
            Department = session.Department,            
            SessionId = sessionId,
            ExpiresIn = expiresIn
        });

        // Rotate refresh token
        var newRefreshToken = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));
        var newRefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(30);

        var updatedSession = new SessionInfo
        {
            SessionId = sessionId,
            UserId = session.UserId,
            UserSecurityId = session.UserSecurityId,
            UserType = session.UserType,
            EntityType = session.EntityType,
            EntityCode = session.EntityCode,
            Department = resolvedDepartment,
            CreatedAtUtc = session.CreatedAtUtc,
            ExpiresAtUtc = newRefreshTokenExpiresAtUtc,
            RefreshToken = newRefreshToken,
            RefreshTokenExpiresAtUtc = newRefreshTokenExpiresAtUtc,
            IpAddress = ipAddress ?? session.IpAddress,
            UserAgent = userAgent ?? session.UserAgent
        };
        await _sessionStore.CreateAsync(updatedSession, cancellationToken);

        return new LoginResult
        {
            Success = true,
            User = new LoginUser
            {
                UserId = user.UserName ?? string.Empty,
                UserSecurityId = user.UserSecurityID,
                FullName = resolvedFullName,
                UserType = user.UserType ?? "",
                Title = resolvedTitle,
                UserSpecialToken = resolvedUserSpecialToken,
                EntityType = updatedSession.EntityType,
                EntityCode = updatedSession.EntityCode,
                Department = resolvedDepartment,
                RespCenter = userGraph.SecondaryCenter?.Code ?? userGraph.DefCenter?.Code ?? defLocation.RespCenter ?? string.Empty,
                WorkDate = workDate,
                Avatar = user.Avatar
            },
            Menus = menus,
            Token = newAccessToken,
            RefreshToken = newRefreshToken,
            Locations = locations,
            RequirePasswordChange = false,
            RequirePasswordChangeReason = null
        };
    }

    public async Task<ProfileResult?> GetProfileAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (userId.IsEmpty())
            return null;

        using var scope = _dataService.ForNavLive();

        var user = await scope.Query<Dataverse.NavLive.User>()
            .Where(u => u.UserName == userId || u.MobileNo == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (user == null)
            return null;

        var setups = (await scope.Query<RespCenterUserSetup>()
            .Where(s => s.UserID == user.UserName)
            .Where(s=>s.Code != "")
            .ToArrayAsync(cancellationToken))
            .Where(s => s.Code.HasValue())
            .ToList();

        // Single round trip for employees + salespersons + responsibility centers
        var entities = await BuildProfileEntitiesBatchAsync(scope, setups, cancellationToken);

        var lastPasswordChanged = user.PasswordChangedDate ?? DateTime.MinValue;
        if (lastPasswordChanged != DateTime.MinValue && (lastPasswordChanged.Year <= 1 || lastPasswordChanged.Year == 1573 || lastPasswordChanged.Year == 1753))
            lastPasswordChanged = DateTime.MinValue;

        return new ProfileResult
        {
            UserId = user.UserName ?? string.Empty,
            FullName = user.FullName ?? string.Empty,
            UserType = user.UserType ?? string.Empty,
            MobileNo = user.MobileNo ?? string.Empty,
            Email = user.AuthenticationEmail ?? string.Empty,
            Avatar = user.Avatar,
            LastPasswordChanged = lastPasswordChanged,
            SecurityPIN = user.SecurityPin,
            Entities = entities
        };
    }

    public async Task<bool> SetProfileAsync(string userId, ProfileUpdateInput input, CancellationToken cancellationToken = default)
    {
        if (userId.IsEmpty() || input == null)
            return false;

        using var scope = _dataService.ForNavLive();

        var user = await scope.Query<Dataverse.NavLive.User>()
            .Where(u => u.UserName == userId || u.MobileNo == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (user == null)
            return false;

        var updates = new List<Expression<Func<Dataverse.NavLive.User, object>>>();

        if (input.FullName != null) { user.FullName = input.FullName; updates.Add(u => u.FullName); }
        if (input.UserType != null) { user.UserType = input.UserType; updates.Add(u => u.UserType); }
        if (input.MobileNo != null) { user.MobileNo = input.MobileNo; updates.Add(u => u.MobileNo); }
        if (input.Email != null) { user.Emails = input.Email; updates.Add(u => u.Emails); }
        if (input.AuthenticationEmail != null) { user.AuthenticationEmail = input.AuthenticationEmail; updates.Add(u => u.AuthenticationEmail); }
        if (input.State.HasValue) { user.State = input.State.Value; updates.Add(u => u.State); }
        if (input.Avatar.HasValue) { user.Avatar = input.Avatar.Value; updates.Add(u => u.Avatar); }
        if (input.SecurityPIN.HasValue) { user.SecurityPin = input.SecurityPIN.Value; updates.Add(u => u.SecurityPin); }
        if (input.CanRunErp.HasValue) { user.CanRunERP = input.CanRunErp.Value; updates.Add(u => u.CanRunERP); }
        if (input.CanRunOldErp.HasValue) { user.CanRunOldERP = input.CanRunOldErp.Value; updates.Add(u => u.CanRunOldERP); }
        if (input.AllowAllMasters.HasValue) { user.AllowAllMasters = input.AllowAllMasters.Value; updates.Add(u => u.AllowAllMasters); }
        if (input.BackupStorageQuotaGB.HasValue) { user.BackupStorageQuotaGB = input.BackupStorageQuotaGB.Value; updates.Add(u => u.BackupStorageQuotaGB); }
        if (input.BackupAllowedFileTypes != null) { user.BackupAllowedFileTypes = input.BackupAllowedFileTypes; updates.Add(u => u.BackupAllowedFileTypes); }
        if (input.BackupGDriveFolderID != null) { user.BackupGDriveFolderID = input.BackupGDriveFolderID; updates.Add(u => u.BackupGDriveFolderID); }

        if (updates.Count > 0)
        {
            await scope.UpdateAsync(user, updates.ToArray(), cancellationToken);
        }
        
        return true;
    }

    /// <summary>Batch-load entities for profile in one round trip: employees, salespersons, responsibility centers.</summary>
    private static async Task<List<UserEntity>> BuildProfileEntitiesBatchAsync(
        ITenantScope scope,
        List<RespCenterUserSetup> setups,
        CancellationToken cancellationToken)
    {
        if (setups.Count == 0)
            return new List<UserEntity>();

        var empCodes = setups
            .Where(s => s.Type == RespCenterUserSetupType.Employee && s.Code.HasValue())
            .Select(s => s.Code!)
            .Distinct()
            .ToList();
        var partnerCodes = setups
            .Where(s => s.Type == RespCenterUserSetupType.Partner && s.Code.HasValue())
            .Select(s => s.Code!)
            .Distinct()
            .ToList();
        var groupCodes = setups
            .Where(s => s.Type == RespCenterUserSetupType.PartnerGroup && s.Code.HasValue())
            .Select(s => s.Code!)
            .Distinct()
            .ToList();
        var rcCodes = setups.Select(s => s.RespCenter).Where(c => c.HasValue()).Distinct().ToList();

        List<Employee> employees;
        List<SalespersonPurchaser> salespersons;
        List<ResponsibilityCenter> responsibilityCenters;

        var multi = scope.CreateMultipleQuery();
        var empQuery = empCodes.Count > 0
            ? scope.Query<Employee>().Where(e => empCodes.Contains(e.No))
            : scope.Query<Employee>().Where(e => e.No == "");
        var spQuery = (partnerCodes.Count > 0 || groupCodes.Count > 0)
            ? scope.Query<SalespersonPurchaser>().Where(s => partnerCodes.Contains(s.Code) || groupCodes.Contains(s.Group))
            : scope.Query<SalespersonPurchaser>().Where(s => s.Code == "");
        var rcQuery = rcCodes.Count > 0
            ? scope.Query<ResponsibilityCenter>().Where(rc => rcCodes.Contains(rc.Code))
            : scope.Query<ResponsibilityCenter>().Where(rc => rc.Code == "");

        (var empArr, var spArr, var rcArr) = await multi.Add(empQuery).Add(spQuery).Add(rcQuery).ExecuteAsync<Employee, SalespersonPurchaser, ResponsibilityCenter>(cancellationToken);
        employees = empArr.ToList();
        salespersons = spArr.ToList();
        responsibilityCenters = rcArr.ToList();

        var empByNo = employees.ToDictionary(e => e.No, StringComparer.OrdinalIgnoreCase);
        var spByCode = salespersons.ToDictionary(s => s.Code, StringComparer.OrdinalIgnoreCase);
        var spByGroup = salespersons
            .Where(sp => sp.Group.HasValue())
            .GroupBy(sp => sp.Group, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        var rcByCode = responsibilityCenters.ToDictionary(rc => rc.Code, StringComparer.OrdinalIgnoreCase);

        var entities = new List<UserEntity>();       
        foreach (var s in setups)
        {
            var location = rcByCode.TryGetValue(s.RespCenter ?? "", out var rc) ? (rc.Name ?? s.RespCenter ?? "") : (s.RespCenter ?? "");            
            if (s.Type == RespCenterUserSetupType.Employee && s.Code.HasValue() && empByNo.TryGetValue(s.Code!, out var emp))
            {                
                entities.Add(new UserEntity
                {
                    Code = s.Code ?? "",
                    Name = emp.Initials ?? "",
                    Title = emp.JobTitle.HasValue() ? emp.JobTitle : (emp.Title ?? ""),
                    Location = location
                });
            }
            else if (s.Type == RespCenterUserSetupType.Partner && s.Code.HasValue() && spByCode.TryGetValue(s.Code!, out var sp))
            {
                entities.Add(new UserEntity
                {
                    Code = s.Code ?? "",
                    Name = sp.Name ?? "",
                    Title = "Dealer",
                    Location = location
                });
            }
            else if (s.Type == RespCenterUserSetupType.PartnerGroup && s.Code.HasValue() && spByGroup.TryGetValue(s.Code!, out var groupList))
            {
                foreach (var salesperson in groupList)
                {
                    entities.Add(new UserEntity
                    {
                        Code = salesperson.Code ?? "",
                        Name = salesperson.Name ?? "",
                        Title = "Dealer",
                        Location = location
                    });
                }
            }
        }

        return entities;
    }

    private static bool IsDealerOrSales(string userType)
    {
        return string.Equals(userType, UserTypeDealer, StringComparison.OrdinalIgnoreCase)
            || string.Equals(userType, UserTypeSales, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<UserLoginRow[]> LoadUserBaseGraphAsync(ITenantScope scope, string username, CancellationToken cancellationToken)
    {
        var query = scope.Query<Dataverse.NavLive.User>()
            .Where(u => u.UserName == username || u.MobileNo == username)
            .Join<RespCenterUserSetup, JoinQuery<Dataverse.NavLive.User, RespCenterUserSetup>>(
                u => u.UserName,
                c => c.UserID,
                uc => uc,
                JoinType.Left)
            .Where(uc => uc.Right.Default == 1)
            .Join<ResponsibilityCenter, JoinQuery<JoinQuery<Dataverse.NavLive.User, RespCenterUserSetup>, ResponsibilityCenter>>(
                uc => uc.Right.RespCenter,
                rc => rc.Code,
                uc_rc => uc_rc,
                JoinType.Left)
            .Join<ResponsibilityCenter, JoinQuery<JoinQuery<JoinQuery<Dataverse.NavLive.User, RespCenterUserSetup>, ResponsibilityCenter>, ResponsibilityCenter>>(
                r => r.Right.RespCenterSales,
                rc => rc.Code,
                r_rc => r_rc,
                JoinType.Left);

        return await query.Select(node => new UserLoginRow
        {
            User = new Dataverse.NavLive.User
            {
                UserSecurityID = node.Left.Left.Left.UserSecurityID,
                UserName = node.Left.Left.Left.UserName,
                State = node.Left.Left.Left.State,
                FullName = node.Left.Left.Left.FullName,
                UserType = node.Left.Left.Left.UserType,
                Salt = node.Left.Left.Left.Salt,
                EncryptedPassword = node.Left.Left.Left.EncryptedPassword,
                ChangePassword = node.Left.Left.Left.ChangePassword,
                PasswordChangedDate = node.Left.Left.Left.PasswordChangedDate,
                Avatar = node.Left.Left.Left.Avatar,
                SecurityPin= node.Left.Left.Left.SecurityPin,

            },
            Setup = new RespCenterUserSetup
            {
                RespCenter = node.Left.Left.Right.RespCenter,
                Type = node.Left.Left.Right.Type,
                Code = node.Left.Left.Right.Code
            },
            DefCenter = new ResponsibilityCenter
            {
                Code = node.Left.Right.Code,
                Name = node.Left.Right.Name,
                SalesClosureEndDate = node.Left.Right.SalesClosureEndDate
            },
            SecondaryCenter = new ResponsibilityCenter
            {
                Code = node.Right.Code,
                Name = node.Right.Name,
                SalesClosureEndDate = node.Right.SalesClosureEndDate
            }
        }).ToArrayAsync(cancellationToken);
    }

    public async Task<string?> ResetPasswordAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (userId.IsEmpty())
            return null;

        using var scope = _dataService.ForNavLive();
        var user = await scope.Query<Dataverse.NavLive.User>()
            .Where(u => u.UserName == userId || u.MobileNo == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (user == null)
            return null;

        string newPassword = DefaultPassword; //Guid.NewGuid().ToString("N").Substring(0, 8);
        var salt = _passwordEncryption.CreateSalt();
        user.Salt = salt;
        user.EncryptedPassword = _passwordEncryption.EncryptPassword(newPassword, salt);
        user.ChangePassword = 1;
        user.PasswordChangedDate = DateTime.Today;

        await scope.UpdateAsync(user, new Expression<Func<Dataverse.NavLive.User, object>>[]
        {
            u => u.Salt,
            u => u.EncryptedPassword,
            u => u.ChangePassword,
            u => u.PasswordChangedDate
        }, cancellationToken);

        return newPassword;
    }

    public async Task<bool> ChangePasswordAsync(string userId, string newPassword, string? oldPassword = null, int? securityPin = null, CancellationToken cancellationToken = default)
    {
        if (userId.IsEmpty() || newPassword.IsEmpty())
            return false;
        if (oldPassword.IsEmpty() && !securityPin.HasValue)
            return false;

        using var scope = _dataService.ForNavLive();
        var user = await scope.Query<Dataverse.NavLive.User>()
            .Where(u => u.UserName == userId || u.MobileNo == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (user == null)
            return false;

        var validByPassword = oldPassword != null && _passwordEncryption.IsPasswordMatch(oldPassword, user.Salt, user.EncryptedPassword);
        var isFirstLogin = !ToPasswordChangedDate(user.PasswordChangedDate).HasValue;
        var validByPin = securityPin.HasValue && (
            isFirstLogin
                ? true
                : user.SecurityPin == securityPin.Value);
        if (!validByPassword && !validByPin)
            return false;

        var salt = _passwordEncryption.CreateSalt();
        user.Salt = salt;
        user.EncryptedPassword = _passwordEncryption.EncryptPassword(newPassword, salt);
        user.ChangePassword = 0;
        user.PasswordChangedDate = DateTime.Today;

        var updates = new List<Expression<Func<Dataverse.NavLive.User, object>>>
        {
            u => u.Salt,
            u => u.EncryptedPassword,
            u => u.ChangePassword,
            u => u.PasswordChangedDate
        };

        if (securityPin.HasValue && isFirstLogin)
        {
            user.SecurityPin = securityPin.Value;
            updates.Add(u => u.SecurityPin);
        }

        await scope.UpdateAsync(user, updates.ToArray(), cancellationToken);

        return true;
    }

    public async Task<bool> ForgotPasswordAsync(string username, int securityPin, string newPassword, CancellationToken cancellationToken = default)
    {
        if (username.IsEmpty() || newPassword.IsEmpty())
            return false;

        using var scope = _dataService.ForNavLive();
        var user = await scope.Query<Dataverse.NavLive.User>()
            .Where(u => u.UserName == username || u.MobileNo == username)
            .FirstOrDefaultAsync(cancellationToken);

        if (user == null || user.SecurityPin != securityPin)
            return false;

        var salt = _passwordEncryption.CreateSalt();
        user.Salt = salt;
        user.EncryptedPassword = _passwordEncryption.EncryptPassword(newPassword, salt);
        user.ChangePassword = 0;
        user.PasswordChangedDate = DateTime.Today;

        await scope.UpdateAsync(user, new Expression<Func<Dataverse.NavLive.User, object>>[]
        {
            u => u.Salt,
            u => u.EncryptedPassword,
            u => u.ChangePassword,
            u => u.PasswordChangedDate
        }, cancellationToken);

        return true;
    }
    public async Task<IReadOnlyList<UserSearchResult>> SearchUsersAsync(string? search, int take = 20, CancellationToken cancellationToken = default)
    {
        using var scope = _dataService.ForNavLive();
        var query = scope.Query<Dataverse.NavLive.User>();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(u => u.UserName.Contains(s) || u.FullName.Contains(s));
        }

        var users = await query
            .Take(take)
            .Select(u => new UserSearchResult
            {
                UserId = u.UserName ?? string.Empty,
                FullName = u.FullName ?? string.Empty,
                UserType = u.UserType ?? string.Empty,
                Avatar = u.Avatar
            })
            .ToArrayAsync(cancellationToken);

        return users.ToList();
    }

    public async Task<UserDetail?> GetUserAsync(string username, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
            return null;

        using var scope = _dataService.ForNavLive();
        var user = await scope.Query<Dataverse.NavLive.User>()
            .Where(u => u.UserName == username || u.MobileNo == username)
            .FirstOrDefaultAsync(cancellationToken);

        if (user == null)
            return null;

        var uname = user.UserName ?? string.Empty;
        var sid = user.UserSecurityID;

        var respRows = await scope.Query<RespCenterUserSetup>()
            .Where(r => r.UserID == uname)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        var postingRows = await scope.Query<UserSetup>()
            .Where(u => u.UserID == uname)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        var accessRows = await scope.Query<AccessControl>()
            .Where(ac => ac.UserSecurityID == sid)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, string> nameByRole = new(StringComparer.OrdinalIgnoreCase);
        if (accessRows.Length > 0)
        {
            var roleIds = accessRows.Select(a => a.RoleID).Where(id => id.HasValue()).Distinct().ToArray();
            if (roleIds.Length > 0)
            {
                // Tyresoles.Sql cannot translate array.Contains(column) (invokes unsupported runtime methods).
                // Same pattern as ProteanDataService.WhereResponsibilityCenterCodesIn.
                var inParams = new Dictionary<string, object>(roleIds.Length);
                var placeholders = new List<string>(roleIds.Length);
                for (var i = 0; i < roleIds.Length; i++)
                {
                    var pname = $"@rid{i}";
                    inParams[pname] = roleIds[i]!;
                    placeholders.Add(pname);
                }

                var sets = await scope.Query<PermissionSet>()
                    .Where($"[Role ID] IN ({string.Join(", ", placeholders)})", inParams)
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false);
                foreach (var p in sets)
                    nameByRole[p.RoleID] = p.Name.HasValue() ? p.Name! : (p.DisplayName.HasValue() ? p.DisplayName! : p.RoleID);
            }
        }

        static string NavDateToUi(DateTime? d) =>
            d is { } x ? x.ToString("yyyy-MM-dd") : string.Empty;

        var respSetup = respRows
            .Select(r => new UserDetailRespCenterRow
            {
                UserId = r.UserID ?? string.Empty,
                RespCenter = r.RespCenter ?? string.Empty,
                RespDefault = r.Default,
                Type = (int)r.Type,
                Code = r.Code ?? string.Empty
            })
            .ToList();

        var postSetup = postingRows
            .Select(p => new UserDetailPostingRow
            {
                ResponsibilityCenter = p.ResponsibilityCenter ?? string.Empty,
                AllowPostingFrom = NavDateToUi(p.AllowPostingFrom),
                AllowPostingTo = NavDateToUi(p.AllowPostingTo)
            })
            .ToList();

        var perms = accessRows
            .Select(ac => new UserDetailPermissionRow
            {
                RoleId = ac.RoleID ?? string.Empty,
                RoleName = nameByRole.TryGetValue(ac.RoleID ?? string.Empty, out var rn) ? rn : string.Empty,
                CompanyName = ac.CompanyName ?? string.Empty,
                AssignerName = ac.AssignerName ?? string.Empty,
                RoleExipryDate = NavDateToUi(ac.RoleExipryDate),
                Values = ac.Values ?? string.Empty,
                HomePath = ac.HomePath
            })
            .ToList();

        return new UserDetail
        {
            UserId = uname,
            FullName = user.FullName ?? string.Empty,
            UserType = user.UserType ?? string.Empty,
            MobileNo = user.MobileNo ?? string.Empty,
            AuthenticationEmail = user.Emails ?? string.Empty,
            State = user.State,
            CanRunERP = user.CanRunERP,
            CanRunOldERP = user.CanRunOldERP,
            AllowAllMasters = user.AllowAllMasters,
            BackupStorageQuotaGB = user.BackupStorageQuotaGB,
            BackupAllowedFileTypes = user.BackupAllowedFileTypes ?? string.Empty,
            BackupGDriveFolderID = user.BackupGDriveFolderID ?? string.Empty,
            RDPPassword = user.RDPPassword,
            NavConfigName = user.NavConfigName,
            VpnUserId = user.VpnUserID,
            VpnPassword = user.VpnPassword,
            RespCenterSetup = respSetup,
            PostingSetup = postSetup,
            Permissions = perms
        };
    }

    public async Task<bool> UpdatePermissionsAsync(string username, List<UserPermissionInput> permissions, CancellationToken cancellationToken = default)
    {
        using var scope = _dataService.ForNavLive();
        var user = await scope.Query<Dataverse.NavLive.User>().Where(u => u.UserName == username).FirstOrDefaultAsync(cancellationToken);
        if (user == null) return false;

        await scope.DeleteWhereAsync<AccessControl>(ac => ac.UserSecurityID == user.UserSecurityID, cancellationToken);
        
        if (permissions.Count > 0)
        {
            var newRecords = permissions.Select(p => new AccessControl
            {
                UserSecurityID = user.UserSecurityID,
                RoleID = p.RoleId,
                Values = p.Values,
                HomePath = p.HomePath,
                AssignerName = "ADMIN", // Or track current admin
                CompanyName = "" // Assuming shared across companies or empty for global
            });
            await scope.BulkInsertAsync(newRecords, cancellationToken);
        }
        return true;
    }

    public async Task<bool> UpdateResponsibilityCentersAsync(string username, List<UserRespCenterInput> assignments, CancellationToken cancellationToken = default)
    {
        using var scope = _dataService.ForNavLive();
        // RespCenterUserSetup uses UserID (string)
        await scope.DeleteWhereAsync<RespCenterUserSetup>(r => r.UserID == username, cancellationToken);

        if (assignments.Count > 0)
        {
            var newRecords = assignments.Select(a => new RespCenterUserSetup
            {
                UserID = username,
                RespCenter = a.RespCenter,
                Default = a.Default,
                Type = a.Type,
                Code = a.Code
            });
            await scope.BulkInsertAsync(newRecords, cancellationToken);
        }
        return true;
    }

    public async Task<bool> UpdatePostingSetupAsync(string username, List<UserPostingSetupInput> assignments, CancellationToken cancellationToken = default)
    {
        using var scope = _dataService.ForNavLive();
        // UserSetup uses UserID (string)
        await scope.DeleteWhereAsync<UserSetup>(u => u.UserID == username, cancellationToken);

        if (assignments.Count > 0)
        {
            var newRecords = assignments.Select(a => new UserSetup
            {
                UserID = username,
                ResponsibilityCenter = a.ResponsibilityCenter,
                AllowPostingFrom = a.AllowPostingFrom,
                AllowPostingTo = a.AllowPostingTo
            });
            await scope.BulkInsertAsync(newRecords, cancellationToken);
        }
        return true;
    }
}

