namespace Tyresoles.Web.Services.Email;

public class AmazonSesSmtpSettings
{
    public const string SectionName = "AmazonSesSmtp";

    public string Host { get; set; } = "email-smtp.ap-south-1.amazonaws.com";
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DefaultFromEmail { get; set; } = "updates@tyresoles.in";
    public string DefaultFromName { get; set; } = "Tyresoles Fleet Solutions";
    public string? DefaultReplyToEmail { get; set; } = "sales@tyresoles.in";
    public string TrackingBaseUrl { get; set; } = "https://app.tyresoles.in";
    public int MaxSendRatePerSecond { get; set; } = 10;
}
