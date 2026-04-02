namespace Tyresoles.Data.Features.Common;

public class NavWebServiceSettings
{
    public const string SectionName = "NavWebService";
    public string UserID { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
