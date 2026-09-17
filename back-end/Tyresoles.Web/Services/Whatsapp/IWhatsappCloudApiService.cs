using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Tyresoles.Data.Features.Crm.Entities;

namespace Tyresoles.Web.Services.Whatsapp;

public class WhatsappSendResult
{
    public bool Success { get; set; }
    public string? Wamid { get; set; }
    public int? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}

public class WhatsappTemplateComponent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "body"; // header, body, button

    [JsonPropertyName("sub_type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SubType { get; set; } // url, quick_reply

    [JsonPropertyName("index")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Index { get; set; }

    [JsonPropertyName("parameters")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<WhatsappParameter>? Parameters { get; set; } = new();
}

public class WhatsappParameter
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text"; // text, image, document, video

    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    [JsonPropertyName("image")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WhatsappMediaParam? Image { get; set; }

    [JsonPropertyName("document")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WhatsappMediaParam? Document { get; set; }

    [JsonPropertyName("video")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WhatsappMediaParam? Video { get; set; }
}

public class WhatsappMediaParam
{
    [JsonPropertyName("link")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Link { get; set; }

    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    [JsonPropertyName("filename")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Filename { get; set; }
}

public class WabaHealthStatus
{
    public bool IsConnected { get; set; }
    public string QualityRating { get; set; } = "UNKNOWN"; // GREEN, YELLOW, RED, UNKNOWN
    public string MessagingLimitTier { get; set; } = "TIER_1K"; // TIER_250, TIER_1K, TIER_10K, TIER_100K, TIER_UNLIMITED
    public string? DisplayPhoneNumber { get; set; }
    public string? VerifiedName { get; set; }
    public int CurrentDayUsage { get; set; }
    public int DailyLimit { get; set; } = 1000;
    public string? StatusMessage { get; set; }
}

public class CreateTemplateInput
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "MARKETING"; // MARKETING, UTILITY, AUTHENTICATION
    public string Language { get; set; } = "en";
    public string HeaderType { get; set; } = "NONE"; // NONE, TEXT, IMAGE, DOCUMENT, VIDEO
    public string? HeaderText { get; set; }
    public string? HeaderSampleUrl { get; set; }
    public string BodyText { get; set; } = string.Empty;
    public string? FooterText { get; set; }
    public List<string>? BodySampleValues { get; set; }
    public string? ButtonsJson { get; set; }
}

public interface IWhatsappCloudApiService
{
    Task<WhatsappSendResult> SendTemplateMessageAsync(
        string toPhone, 
        string templateName, 
        string languageCode, 
        List<WhatsappTemplateComponent>? components, 
        CancellationToken ct = default);

    Task<WhatsappSendResult> SendDirectTextMessageAsync(
        string toPhone, 
        string messageText, 
        CancellationToken ct = default);

    Task<List<CrmWhatsappTemplate>> SyncTemplatesFromMetaAsync(CancellationToken ct = default);

    Task<CrmWhatsappTemplate> CreateTemplateOnMetaAsync(CreateTemplateInput input, CancellationToken ct = default);

    Task<WabaHealthStatus> GetWabaHealthStatusAsync(CancellationToken ct = default);

    bool VerifyWebhookSignature(string rawPayload, string signatureHeader);
}
