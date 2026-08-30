namespace Tyresoles.Data.Features.Admin.EmailAccounts;

public class RediffmailSettings
{
    public const string SectionName = "RediffmailPro";

    public string BaseUrl { get; set; } = "https://api.rediffmailpro.com/eproadminapi/";
    public string AdminLogin { get; set; } = "";
    public string AdminPassword { get; set; } = "";
    public string Domain { get; set; } = "";
    public string AccountType { get; set; } = "0";
}
