namespace Tyresoles.Web.Services.Whatsapp;

public class WhatsappSettings
{
    public string? WabaId { get; set; }
    public string? PhoneNumberId { get; set; }
    public string? DisplayPhoneNumber { get; set; }
    public string? AccessToken { get; set; }
    public string? AppSecret { get; set; }
    public string WebhookVerifyToken { get; set; } = "tyresoles_crm_wa_webhook_verify_2026";
    public int MaxSendRatePerSecond { get; set; } = 30;
    public bool SimulationMode { get; set; } = false;
    public decimal CostPerMessage { get; set; } = 0.80m;
}
