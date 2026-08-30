using System.Net.Http.Headers;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Tyresoles.Data.Features.Admin.EmailAccounts;

public class RediffmailService : IRediffmailService
{
    private readonly HttpClient _httpClient;
    private readonly RediffmailSettings _settings;
    private readonly ILogger<RediffmailService> _logger;

    private AdminAuthResult? _cachedAuth;
    private DateTime _authTime = DateTime.MinValue;

    public RediffmailService(
        HttpClient httpClient,
        IOptions<RediffmailSettings> settings,
        ILogger<RediffmailService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<AdminAuthResult> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedAuth != null && _cachedAuth.IsSuccess && (DateTime.UtcNow - _authTime).TotalMinutes < 30)
        {
            return _cachedAuth;
        }

        if (string.IsNullOrWhiteSpace(_settings.AdminLogin) || string.IsNullOrWhiteSpace(_settings.AdminPassword))
        {
            return new AdminAuthResult
            {
                IsSuccess = false,
                Error = "Rediffmail Pro admin credentials are not configured. Please set 'RediffmailPro:AdminLogin' and 'RediffmailPro:AdminPassword' in appsettings.json."
            };
        }

        try
        {
            var requestUrl = BuildUrl("LoginUser");
            var form = new Dictionary<string, string>
            {
                { "FormName", "existing" },
                { "login", _settings.AdminLogin },
                { "passwd", _settings.AdminPassword },
                { "output", "json" },
                { "remember", "1" }
            };

            var req = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = new FormUrlEncodedContent(form)
            };
            req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

            var response = await _httpClient.SendAsync(req, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogInformation("Rediffmail Login response: {Response}", content);

            try
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                if (root.TryGetProperty("BODY", out var body) && body.TryGetProperty("Rmail", out var rmail))
                {
                    var msg = rmail.TryGetProperty("Msg", out var msgProp) ? msgProp.GetString() : null;
                    var status = rmail.TryGetProperty("Status", out var statusProp) ? statusProp.GetString() : null;
                    var rm = rmail.TryGetProperty("Rm", out var rmProp) ? rmProp.GetString() : null;
                    var rl = rmail.TryGetProperty("Rl", out var rlProp) ? rlProp.GetString() : null;
                    var rsc = rmail.TryGetProperty("Rsc", out var rscProp) ? rscProp.GetString() : null;
                    var rt = rmail.TryGetProperty("Rt", out var rtProp) ? rtProp.GetString() : null;

                    if (string.Equals(msg, "SUCCESS", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(rsc))
                    {
                        _cachedAuth = new AdminAuthResult
                        {
                            IsSuccess = true,
                            Status = status,
                            Msg = msg,
                            Rm = rm,
                            Rl = rl,
                            Rsc = rsc,
                            Rt = rt
                        };
                        _authTime = DateTime.UtcNow;
                        return _cachedAuth;
                    }

                    return new AdminAuthResult
                    {
                        IsSuccess = false,
                        Status = status,
                        Msg = msg,
                        Error = $"Rediffmail login failed: {msg ?? "Invalid credentials"}"
                    };
                }
            }
            catch (JsonException)
            {
                // Fallback for non-JSON or HTML response
            }

            return new AdminAuthResult
            {
                IsSuccess = false,
                Error = $"Rediffmail login response format invalid. Raw response: {content.Substring(0, Math.Min(200, content.Length))}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error authenticating with Rediffmail Pro API");
            return new AdminAuthResult
            {
                IsSuccess = false,
                Error = ex.Message
            };
        }
    }

    private HttpRequestMessage CreateAuthenticatedRequest(string endpoint, Dictionary<string, string> formParams, AdminAuthResult auth)
    {
        var url = BuildUrl(endpoint);
        
        // Always pass login, output, session_id
        if (!formParams.ContainsKey("login") && !string.IsNullOrEmpty(_settings.AdminLogin))
        {
            formParams["login"] = _settings.AdminLogin;
        }
        if (!formParams.ContainsKey("output"))
        {
            formParams["output"] = "json";
        }
        if (!formParams.ContainsKey("session_id") && !string.IsNullOrEmpty(auth.SessionId))
        {
            formParams["session_id"] = auth.SessionId;
        }

        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(formParams)
        };

        // Attach Cookies: Rm, Rsc, Rl, accounttype, Rt
        var cookieHeader = $"Rm={auth.Rm}; Rsc={auth.Rsc}; Rl={auth.Rl}; accounttype={_settings.AccountType}; Rt={auth.Rt}";
        req.Headers.Add("Cookie", cookieHeader);
        req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

        return req;
    }

    public async Task<EmailOperationResult> CreateUserAsync(CreateEmailAccountInput input, CancellationToken cancellationToken = default)
    {
        var auth = await AuthenticateAsync(cancellationToken);
        if (!auth.IsSuccess)
        {
            return new EmailOperationResult { Success = false, Error = auth.Error };
        }

        var form = new Dictionary<string, string>
        {
            { "domain_name", _settings.Domain },
            { "userdomainname", _settings.Domain },
            { "userid", input.UserId },
            { "passwd", input.Password },
            { "fname", input.FirstName },
            { "sname", input.LastName },
            { "nickname", input.Nickname ?? $"{input.FirstName} {input.LastName}" },
            { "code", input.EmployeeCode },
            { "mobile", input.Mobile },
            { "pwd_change_at_firstlogin", input.PwdChangeAtFirstLogin },
            { "userSpace", input.UserSpaceMb.ToString() },
            { "day", input.Day ?? "" },
            { "month", input.Month ?? "" },
            { "year", input.Year ?? "" },
            { "branch", input.Branch ?? "" },
            { "city", input.City ?? "" },
            { "altemail", input.AltEmail ?? "" },
            { "status", "A" },
            { "segment", "1" },
            { "designation", input.Designation ?? "" },
            { "department", input.Department ?? "" },
            { "org_name", input.OrgName ?? "Tyresoles" },
            { "url", input.Url ?? "" },
            { "role", input.Role ?? "" },
            { "note", input.Note ?? "" },
            { "timezone", input.Timezone },
            { "address", input.Address ?? "" },
            { "state", input.State ?? "" },
            { "zip", input.Zip ?? "" },
            { "country_code", input.CountryCode },
            { "ph_work", input.PhWork ?? "" },
            { "ph_home", input.PhHome ?? "" },
            { "fax", input.Fax ?? "" },
            { "add_user.x", "32" },
            { "add_user.y", "11" },
            { "controller_action", "addUser" }
        };

        if (input.MailingListIds != null && input.MailingListIds.Count > 0)
        {
            for (int i = 0; i < input.MailingListIds.Count; i++)
            {
                form[$"mailingListId[{i}]"] = input.MailingListIds[i];
            }
        }

        var req = CreateAuthenticatedRequest("AddUser", form, auth);
        return await ExecuteOperationAsync(req, cancellationToken);
    }

    public async Task<EmailOperationResult> UpdateUserAsync(UpdateEmailAccountInput input, CancellationToken cancellationToken = default)
    {
        var auth = await AuthenticateAsync(cancellationToken);
        if (!auth.IsSuccess)
        {
            return new EmailOperationResult { Success = false, Error = auth.Error };
        }

        var form = new Dictionary<string, string>
        {
            { "controller_action", "confirm" },
            { "userid", input.UserId },
            { "fname", input.FirstName ?? "" },
            { "sname", input.LastName ?? "" },
            { "nickname", input.Nickname ?? "" },
            { "code", input.EmployeeCode ?? "" },
            { "mobile", input.Mobile ?? "" },
            { "day", input.Day ?? "" },
            { "month", input.Month ?? "" },
            { "year", input.Year ?? "" },
            { "branch", input.Branch ?? "" },
            { "city", input.City ?? "" },
            { "altemail", input.AltEmail ?? "" },
            { "status", "A" },
            { "designation", input.Designation ?? "" },
            { "department", input.Department ?? "" },
            { "org_name", input.OrgName ?? "" },
            { "url", input.Url ?? "" },
            { "role", input.Role ?? "" },
            { "note", input.Note ?? "" },
            { "timezone", input.Timezone ?? "Asia/Kolkata" },
            { "address", input.Address ?? "" },
            { "state", input.State ?? "" },
            { "zip", input.Zip ?? "" },
            { "country_code", input.CountryCode ?? "91" },
            { "ph_work", input.PhWork ?? "" },
            { "ph_home", input.PhHome ?? "" },
            { "fax", input.Fax ?? "" }
        };

        if (input.MailingListIds != null && input.MailingListIds.Count > 0)
        {
            for (int i = 0; i < input.MailingListIds.Count; i++)
            {
                form[$"mailingListId[{i}]"] = input.MailingListIds[i];
            }
        }

        var req = CreateAuthenticatedRequest("UpdateUser", form, auth);
        return await ExecuteOperationAsync(req, cancellationToken);
    }

    public async Task<EmailOperationResult> DeleteUserAsync(string userEmail, CancellationToken cancellationToken = default)
    {
        var auth = await AuthenticateAsync(cancellationToken);
        if (!auth.IsSuccess)
        {
            return new EmailOperationResult { Success = false, Error = auth.Error };
        }

        var delEmail = userEmail.Contains("@") ? userEmail : $"{userEmail}@{_settings.Domain}";

        var form = new Dictionary<string, string>
        {
            { "del_user", delEmail },
            { "controller_action", "Delete" }
        };

        var req = CreateAuthenticatedRequest("DeleteUser", form, auth);
        return await ExecuteOperationAsync(req, cancellationToken);
    }

    public async Task<EmailOperationResult> ChangePasswordAsync(ChangeEmailPasswordInput input, CancellationToken cancellationToken = default)
    {
        var auth = await AuthenticateAsync(cancellationToken);
        if (!auth.IsSuccess)
        {
            return new EmailOperationResult { Success = false, Error = auth.Error };
        }

        var userIdClean = input.UserId.Contains("@") ? input.UserId.Split('@')[0] : input.UserId;

        var form = new Dictionary<string, string>
        {
            { "userid", userIdClean },
            { "passwd", input.NewPassword },
            { "controller_action", "changePassword" }
        };

        var req = CreateAuthenticatedRequest("ChangePassword", form, auth);
        return await ExecuteOperationAsync(req, cancellationToken);
    }

    public async Task<EmailOperationResult> UpdateUserStatusAsync(UpdateEmailStatusInput input, CancellationToken cancellationToken = default)
    {
        var auth = await AuthenticateAsync(cancellationToken);
        if (!auth.IsSuccess)
        {
            return new EmailOperationResult { Success = false, Error = auth.Error };
        }

        var form = new Dictionary<string, string>
        {
            { "controller", input.Status }, // Active, Inactive, Deactive
            { "userdomainname", _settings.Domain }
        };

        if (input.UserIds != null)
        {
            for (int i = 0; i < input.UserIds.Count; i++)
            {
                var cleanId = input.UserIds[i].Contains("@") ? input.UserIds[i].Split('@')[0] : input.UserIds[i];
                form[$"userids[{i}]"] = cleanId;
            }
        }

        if (input.EmployeeCodes != null)
        {
            for (int i = 0; i < input.EmployeeCodes.Count; i++)
            {
                form[$"employee_codes[{i}]"] = input.EmployeeCodes[i];
            }
        }

        var req = CreateAuthenticatedRequest("UpdateUserStatus", form, auth);
        return await ExecuteOperationAsync(req, cancellationToken);
    }

    public async Task<RediffUserContactDto?> GetUserDetailsAsync(string? userId, string? employeeCode, CancellationToken cancellationToken = default)
    {
        var auth = await AuthenticateAsync(cancellationToken);
        if (!auth.IsSuccess) return null;

        var form = new Dictionary<string, string>
        {
            { "controller", "edit" },
            { "userdomainname", _settings.Domain }
        };

        if (!string.IsNullOrEmpty(userId))
        {
            var cleanId = userId.Contains("@") ? userId.Split('@')[0] : userId;
            form["userid"] = cleanId;
        }

        if (!string.IsNullOrEmpty(employeeCode))
        {
            form["employee_code"] = employeeCode;
        }

        var req = CreateAuthenticatedRequest("GetUserDetails", form, auth);
        var response = await _httpClient.SendAsync(req, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogInformation("GetUserDetails response: {Response}", content);

        if (string.IsNullOrWhiteSpace(content)) return null;

        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (root.TryGetProperty("Contact", out var contactProp))
            {
                return JsonSerializer.Deserialize<RediffUserContactDto>(contactProp.GetRawText());
            }

            if (root.TryGetProperty("UserDetails", out var detailsProp) &&
                detailsProp.TryGetProperty("User", out var userArr) &&
                userArr.ValueKind == JsonValueKind.Array && userArr.GetArrayLength() > 0)
            {
                var firstUser = userArr[0];
                if (firstUser.TryGetProperty("Contact", out var cProp))
                {
                    return JsonSerializer.Deserialize<RediffUserContactDto>(cProp.GetRawText());
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse JSON user details, trying XML fallback");
        }

        // XML Fallback Parsing
        if (content.TrimStart().StartsWith("<"))
        {
            try
            {
                var xml = XDocument.Parse(content);
                var contactElem = xml.Descendants("Contact").FirstOrDefault();
                if (contactElem != null)
                {
                    return new RediffUserContactDto
                    {
                        Email = contactElem.Element("Email")?.Value ?? contactElem.Element("email")?.Value,
                        Fname = contactElem.Element("fname")?.Value ?? contactElem.Element("FirstName")?.Value,
                        Sname = contactElem.Element("sname")?.Value ?? contactElem.Element("LastName")?.Value,
                        Nickname = contactElem.Element("nickname")?.Value,
                        Code = contactElem.Element("code")?.Value,
                        Day = contactElem.Element("day")?.Value,
                        Month = contactElem.Element("month")?.Value,
                        Year = contactElem.Element("year")?.Value,
                        Branch = contactElem.Element("branch")?.Value,
                        Mobile = contactElem.Element("mobile")?.Value,
                        City = contactElem.Element("city")?.Value,
                        Altemail = contactElem.Element("altemail")?.Value,
                        Status = contactElem.Element("status")?.Value,
                        Designation = contactElem.Element("designation")?.Value,
                        Department = contactElem.Element("department")?.Value,
                        Role = contactElem.Element("role")?.Value,
                        Org_name = contactElem.Element("org_name")?.Value,
                        Url = contactElem.Element("url")?.Value,
                        Note = contactElem.Element("note")?.Value,
                        Timezone = contactElem.Element("timezone")?.Value,
                        Address = contactElem.Element("address")?.Value,
                        State = contactElem.Element("state")?.Value,
                        Zip = contactElem.Element("zip")?.Value,
                        Country_code = contactElem.Element("country_code")?.Value,
                        Ph_work = contactElem.Element("ph_work")?.Value,
                        Ph_home = contactElem.Element("ph_home")?.Value,
                        Fax = contactElem.Element("fax")?.Value,
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed XML fallback parsing");
            }
        }

        return null;
    }

    public async Task<EmailOperationResult> AddGlobalAddressContactAsync(AddGlobalContactInput input, CancellationToken cancellationToken = default)
    {
        var auth = await AuthenticateAsync(cancellationToken);
        if (!auth.IsSuccess)
        {
            return new EmailOperationResult { Success = false, Error = auth.Error };
        }

        var form = new Dictionary<string, string>
        {
            { "fname", input.FirstName },
            { "sname", input.LastName },
            { "nickname", input.Nickname ?? $"{input.FirstName} {input.LastName}" },
            { "emailid", input.Email },
            { "designation", input.Designation ?? "" },
            { "department", input.Department ?? "" },
            { "role", input.Role ?? "" },
            { "mobile", input.Mobile ?? "" },
            { "ph_work", input.PhWork ?? "" },
            { "ph_home", input.PhHome ?? "" },
            { "fax", input.Fax ?? "" },
            { "address", input.Address ?? "" },
            { "city", input.City ?? "" },
            { "state", input.State ?? "" },
            { "zip", input.Zip ?? "" },
            { "country_code", input.CountryCode },
            { "org_name", input.OrgName ?? "Tyresoles" },
            { "url", input.Url ?? "" },
            { "note", input.Note ?? "" },
            { "timezone", input.Timezone },
            { "addEmail", "Add Email" },
            { "domain", _settings.Domain },
            { "controller_action", "" }
        };

        var req = CreateAuthenticatedRequest("AddGlobalAddressContact", form, auth);
        return await ExecuteOperationAsync(req, cancellationToken);
    }

    public async Task<GlobalAddressBookResult> GetGlobalAddressContactAsync(CancellationToken cancellationToken = default)
    {
        var auth = await AuthenticateAsync(cancellationToken);
        if (!auth.IsSuccess)
        {
            return new GlobalAddressBookResult { Success = false, Error = auth.Error };
        }

        var form = new Dictionary<string, string>
        {
            { "controller", "showaddrbook" },
            { "controller_action", "getglbaddrbk" },
            { "all", "1" },
            { "sortfield", "0" }
        };

        var req = CreateAuthenticatedRequest("GetGlobalAddressContact", form, auth);
        var response = await _httpClient.SendAsync(req, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogInformation("GetGlobalAddressContact response: {Response}", content);

        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            var status = root.TryGetProperty("Status", out var statusProp) ? statusProp.GetString() : null;
            var action = root.TryGetProperty("Action", out var actionProp) ? actionProp.GetString() : null;

            if (string.Equals(status, "Success", StringComparison.OrdinalIgnoreCase) &&
                root.TryGetProperty("Contact", out var contactProp) &&
                contactProp.ValueKind == JsonValueKind.Array)
            {
                var contacts = JsonSerializer.Deserialize<List<GlobalContactDto>>(contactProp.GetRawText()) ?? new();
                return new GlobalAddressBookResult
                {
                    Success = true,
                    Action = action,
                    Contacts = contacts
                };
            }

            var err = root.TryGetProperty("ERROR", out var errProp) ? errProp.GetString() : null;
            return new GlobalAddressBookResult
            {
                Success = false,
                Action = action,
                Error = err ?? "Failed to retrieve global address book"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Global Address Book");
            return new GlobalAddressBookResult { Success = false, Error = ex.Message };
        }
    }

    private async Task<EmailOperationResult> ExecuteOperationAsync(HttpRequestMessage req, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.SendAsync(req, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogInformation("Rediffmail API response: {Response}", content);

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            var status = root.TryGetProperty("Status", out var statusProp) ? statusProp.GetString() : null;
            var action = root.TryGetProperty("Action", out var actionProp) ? actionProp.GetString() : null;
            var message = root.TryGetProperty("Message", out var msgProp) ? msgProp.GetString() : null;
            var error = root.TryGetProperty("ERROR", out var errProp) ? errProp.GetString() : null;

            RediffUserContactDto? contact = null;
            if (root.TryGetProperty("Contact", out var contactElement))
            {
                try
                {
                    contact = JsonSerializer.Deserialize<RediffUserContactDto>(contactElement.GetRawText());
                }
                catch { }
            }

            var isSuccess = string.Equals(status, "Success", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(action, "Activated", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(action, "Deactivated", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(action, "Add User", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(action, "Edit User", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(action, "Delete User", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(action, "Change Password", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(action, "Add Global Address User", StringComparison.OrdinalIgnoreCase);

            return new EmailOperationResult
            {
                Success = isSuccess,
                Action = action,
                Message = message,
                Error = isSuccess ? null : (error ?? message ?? "API operation failed"),
                Contact = contact
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP call execution failed for Rediffmail Pro API");
            return new EmailOperationResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private string BuildUrl(string endpoint)
    {
        var baseUri = _settings.BaseUrl.TrimEnd('/') + "/";
        return baseUri + endpoint;
    }
}
