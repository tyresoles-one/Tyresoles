using System.Text.Json.Serialization;

namespace Tyresoles.Data.Features.Admin.EmailAccounts;

public class AdminAuthResult
{
    public bool IsSuccess { get; set; }
    public string? Status { get; set; }
    public string? Msg { get; set; }
    public string? Rm { get; set; }
    public string? Rl { get; set; }
    public string? Rsc { get; set; }
    public string? Rt { get; set; }
    public string? SessionId => Rsc;
    public string? Error { get; set; }
}

public class RediffUserContactDto
{
    [JsonPropertyName("Email")]
    public string? Email { get; set; }

    [JsonPropertyName("fname")]
    public string? Fname { get; set; }
    public string? FirstName { get => Fname; set => Fname = value; }

    [JsonPropertyName("sname")]
    public string? Sname { get; set; }
    public string? LastName { get => Sname; set => Sname = value; }

    [JsonPropertyName("nickname")]
    public string? Nickname { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }
    public string? EmployeeCode { get => Code; set => Code = value; }

    [JsonPropertyName("day")]
    public string? Day { get; set; }

    [JsonPropertyName("month")]
    public string? Month { get; set; }

    [JsonPropertyName("year")]
    public string? Year { get; set; }

    [JsonPropertyName("branch")]
    public string? Branch { get; set; }

    [JsonPropertyName("mobile")]
    public string? Mobile { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("altemail")]
    public string? Altemail { get; set; }
    public string? AltEmail { get => Altemail; set => Altemail = value; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("designation")]
    public string? Designation { get; set; }

    [JsonPropertyName("department")]
    public string? Department { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("org_name")]
    public string? Org_name { get; set; }
    public string? OrgName { get => Org_name; set => Org_name = value; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("timezone")]
    public string? Timezone { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("zip")]
    public string? Zip { get; set; }

    [JsonPropertyName("country_code")]
    public string? Country_code { get; set; }
    public string? CountryCode { get => Country_code; set => Country_code = value; }

    [JsonPropertyName("ph_work")]
    public string? Ph_work { get; set; }
    public string? PhWork { get => Ph_work; set => Ph_work = value; }

    [JsonPropertyName("ph_home")]
    public string? Ph_home { get; set; }
    public string? PhHome { get => Ph_home; set => Ph_home = value; }

    [JsonPropertyName("fax")]
    public string? Fax { get; set; }

    public int? UserSpaceMb { get; set; }
}

public class CreateEmailAccountInput
{
    public required string UserId { get; set; } // without domain
    public required string Password { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? Nickname { get; set; }
    public required string EmployeeCode { get; set; }
    public required string Mobile { get; set; }
    public int UserSpaceMb { get; set; } = 1024;
    public string PwdChangeAtFirstLogin { get; set; } = "N";
    public string? Day { get; set; }
    public string? Month { get; set; }
    public string? Year { get; set; }
    public string? Branch { get; set; }
    public string? City { get; set; }
    public string? AltEmail { get; set; }
    public string? Designation { get; set; }
    public string? Department { get; set; }
    public string? OrgName { get; set; }
    public string? Url { get; set; }
    public string? Role { get; set; }
    public string? Note { get; set; }
    public string Timezone { get; set; } = "Asia/Kolkata";
    public string? Address { get; set; }
    public string? State { get; set; }
    public string? Zip { get; set; }
    public string CountryCode { get; set; } = "91";
    public string? PhWork { get; set; }
    public string? PhHome { get; set; }
    public string? Fax { get; set; }
    public List<string>? MailingListIds { get; set; }
}

public class UpdateEmailAccountInput
{
    public required string UserId { get; set; } // without domain
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Nickname { get; set; }
    public string? EmployeeCode { get; set; }
    public string? Mobile { get; set; }
    public string? Day { get; set; }
    public string? Month { get; set; }
    public string? Year { get; set; }
    public string? Branch { get; set; }
    public string? City { get; set; }
    public string? AltEmail { get; set; }
    public string? Designation { get; set; }
    public string? Department { get; set; }
    public string? OrgName { get; set; }
    public string? Url { get; set; }
    public string? Role { get; set; }
    public string? Note { get; set; }
    public string? Timezone { get; set; }
    public string? Address { get; set; }
    public string? State { get; set; }
    public string? Zip { get; set; }
    public string? CountryCode { get; set; }
    public string? PhWork { get; set; }
    public string? PhHome { get; set; }
    public string? Fax { get; set; }
    public List<string>? MailingListIds { get; set; }
}

public class ChangeEmailPasswordInput
{
    public required string UserId { get; set; } // user id without domain or full email
    public required string NewPassword { get; set; }
}

public class UpdateEmailStatusInput
{
    public required List<string> UserIds { get; set; }
    public List<string>? EmployeeCodes { get; set; }
    public required string Status { get; set; } // Active, Inactive, Deactive
}

public class AddGlobalContactInput
{
    public required string Email { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? Nickname { get; set; }
    public string? Mobile { get; set; }
    public string? Designation { get; set; }
    public string? Department { get; set; }
    public string? Role { get; set; }
    public string? OrgName { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Zip { get; set; }
    public string CountryCode { get; set; } = "91";
    public string? PhWork { get; set; }
    public string? PhHome { get; set; }
    public string? Fax { get; set; }
    public string? Url { get; set; }
    public string? Note { get; set; }
    public string Timezone { get; set; } = "Asia/Kolkata";
}

public class GlobalContactDto
{
    [JsonPropertyName("Nickname")]
    public string? Nickname { get; set; }

    [JsonPropertyName("Email")]
    public string? Email { get; set; }

    [JsonPropertyName("FirstName")]
    public string? FirstName { get; set; }

    [JsonPropertyName("LastName")]
    public string? LastName { get; set; }

    [JsonPropertyName("Department")]
    public string? Department { get; set; }

    [JsonPropertyName("Designation")]
    public string? Designation { get; set; }
}

public class EmailOperationResult
{
    public bool Success { get; set; }
    public string? Action { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
    public RediffUserContactDto? Contact { get; set; }
}

public class GlobalAddressBookResult
{
    public bool Success { get; set; }
    public string? Action { get; set; }
    public string? Error { get; set; }
    public List<GlobalContactDto> Contacts { get; set; } = new();
}
