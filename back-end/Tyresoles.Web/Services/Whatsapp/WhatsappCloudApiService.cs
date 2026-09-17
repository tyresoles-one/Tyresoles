using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tyresoles.Data.Features.Crm;
using Tyresoles.Data.Features.Crm.Entities;

namespace Tyresoles.Web.Services.Whatsapp;

public class WhatsappCloudApiService : IWhatsappCloudApiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CrmDbContext _db;
    private readonly WhatsappSettings _defaultSettings;
    private readonly ILogger<WhatsappCloudApiService> _logger;

    private const string GraphApiBaseUrl = "https://graph.facebook.com/v22.0";

    public WhatsappCloudApiService(
        IHttpClientFactory httpClientFactory,
        CrmDbContext db,
        IOptions<WhatsappSettings> defaultSettings,
        ILogger<WhatsappCloudApiService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _db = db;
        _defaultSettings = defaultSettings.Value;
        _logger = logger;
    }

    private async Task<(string? wabaId, string? phoneId, string? token, string? appSecret, bool isSimulated)> ResolveConfigAsync(CancellationToken ct)
    {
        var settings = await _db.CrmSettings
            .Where(s => s.Key.StartsWith("WHATSAPP_"))
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        string? wabaId = settings.GetValueOrDefault("WHATSAPP_WABA_ID") ?? _defaultSettings.WabaId;
        string? phoneId = settings.GetValueOrDefault("WHATSAPP_PHONE_NUMBER_ID") ?? _defaultSettings.PhoneNumberId;
        string? token = settings.GetValueOrDefault("WHATSAPP_ACCESS_TOKEN") ?? _defaultSettings.AccessToken;
        string? appSecret = settings.GetValueOrDefault("WHATSAPP_APP_SECRET") ?? _defaultSettings.AppSecret;
        
        bool sim = _defaultSettings.SimulationMode;
        if (settings.TryGetValue("WHATSAPP_SIMULATION_MODE", out var simStr) && bool.TryParse(simStr, out var parsedSim))
        {
            sim = parsedSim;
        }

        bool missingLiveCreds = string.IsNullOrWhiteSpace(phoneId) || string.IsNullOrWhiteSpace(token);
        return (wabaId, phoneId, token, appSecret, sim || missingLiveCreds);
    }

    public async Task<WhatsappSendResult> SendTemplateMessageAsync(
        string toPhone,
        string templateName,
        string languageCode,
        List<WhatsappTemplateComponent>? components,
        CancellationToken ct = default)
    {
        var (wabaId, phoneId, token, _, isSimulated) = await ResolveConfigAsync(ct);

        // Standardize recipient phone
        var cleanPhone = NormalizePhoneNumber(toPhone);
        if (string.IsNullOrEmpty(cleanPhone))
        {
            return new WhatsappSendResult
            {
                Success = false,
                ErrorCode = 131026,
                ErrorMessage = "Invalid phone number format."
            };
        }

        // SIMULATION MODE
        if (isSimulated)
        {
            _logger.LogInformation("[Simulated] WhatsApp template {Template} dispatched to {Phone}", templateName, cleanPhone);
            return new WhatsappSendResult
            {
                Success = true,
                Wamid = $"wamid.simulated_{Guid.NewGuid():N}"
            };
        }

        // LIVE META GRAPH API CALL
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var templateDict = new Dictionary<string, object>
            {
                ["name"] = templateName,
                ["language"] = new { code = string.IsNullOrEmpty(languageCode) ? "en" : languageCode }
            };

            if (components != null && components.Count > 0)
            {
                templateDict["components"] = components;
            }

            var payload = new Dictionary<string, object>
            {
                ["messaging_product"] = "whatsapp",
                ["recipient_type"] = "individual",
                ["to"] = cleanPhone,
                ["type"] = "template",
                ["template"] = templateDict
            };

            var serializerOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var rawJson = JsonSerializer.Serialize(payload, serializerOptions);
            var jsonContent = new StringContent(rawJson, Encoding.UTF8, "application/json");
            var url = $"{GraphApiBaseUrl}/{phoneId}/messages";

            var response = await client.PostAsync(url, jsonContent, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("messages", out var msgs) && msgs.GetArrayLength() > 0)
                {
                    var msgId = msgs[0].GetProperty("id").GetString();
                    return new WhatsappSendResult
                    {
                        Success = true,
                        Wamid = msgId
                    };
                }
                return new WhatsappSendResult { Success = true };
            }
            else
            {
                int errorCode = (int)response.StatusCode;
                string errorMessage = responseBody;

                try
                {
                    using var errDoc = JsonDocument.Parse(responseBody);
                    if (errDoc.RootElement.TryGetProperty("error", out var errObj))
                    {
                        if (errObj.TryGetProperty("code", out var c)) errorCode = c.GetInt32();
                        if (errObj.TryGetProperty("message", out var m)) errorMessage = m.GetString() ?? responseBody;
                    }
                }
                catch { }

                _logger.LogWarning("Meta WhatsApp send error: {Code} - {Msg}. Request payload was: {Payload}", errorCode, errorMessage, rawJson);
                return new WhatsappSendResult
                {
                    Success = false,
                    ErrorCode = errorCode,
                    ErrorMessage = errorMessage
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling Meta WhatsApp API for recipient {Phone}", cleanPhone);
            return new WhatsappSendResult
            {
                Success = false,
                ErrorCode = 500,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<WhatsappSendResult> SendDirectTextMessageAsync(
        string toPhone,
        string messageText,
        CancellationToken ct = default)
    {
        var (_, phoneId, token, _, isSimulated) = await ResolveConfigAsync(ct);
        var cleanPhone = NormalizePhoneNumber(toPhone);

        if (isSimulated)
        {
            _logger.LogInformation("[Simulated] Direct text message sent to {Phone}: {Text}", cleanPhone, messageText);
            return new WhatsappSendResult { Success = true, Wamid = $"wamid.simulated_direct_{Guid.NewGuid():N}" };
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var payload = new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = cleanPhone,
                type = "text",
                text = new { preview_url = false, body = messageText }
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{GraphApiBaseUrl}/{phoneId}/messages", jsonContent, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(responseBody);
                var msgId = doc.RootElement.GetProperty("messages")[0].GetProperty("id").GetString();
                return new WhatsappSendResult { Success = true, Wamid = msgId };
            }

            return new WhatsappSendResult { Success = false, ErrorMessage = responseBody };
        }
        catch (Exception ex)
        {
            return new WhatsappSendResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<List<CrmWhatsappTemplate>> SyncTemplatesFromMetaAsync(CancellationToken ct = default)
    {
        var (wabaId, _, token, _, isSimulated) = await ResolveConfigAsync(ct);

        if (isSimulated || string.IsNullOrEmpty(wabaId) || string.IsNullOrEmpty(token))
        {
            _logger.LogInformation("Simulating Meta Template Sync (no active live WABA configured).");
            // Return existing DB templates
            return await _db.CrmWhatsappTemplates.OrderByDescending(t => t.CreatedAt).ToListAsync(ct);
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var url = $"{GraphApiBaseUrl}/{wabaId}/message_templates?limit=100";
            var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to query Meta message templates. Status: {Status}", response.StatusCode);
                return await _db.CrmWhatsappTemplates.ToListAsync(ct);
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("data", out var dataArr))
            {
                return await _db.CrmWhatsappTemplates.ToListAsync(ct);
            }

            var existingTemplates = await _db.CrmWhatsappTemplates.ToListAsync(ct);
            var syncedTemplates = new List<CrmWhatsappTemplate>();

            foreach (var item in dataArr.EnumerateArray())
            {
                var name = item.GetProperty("name").GetString() ?? "";
                var status = item.TryGetProperty("status", out var s) ? (s.GetString() ?? "APPROVED").ToUpperInvariant() : "APPROVED";
                var category = item.TryGetProperty("category", out var cat) ? (cat.GetString() ?? "MARKETING").ToUpperInvariant() : "MARKETING";
                var language = item.TryGetProperty("language", out var lang) ? lang.GetString() ?? "en" : "en";
                var metaId = item.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;

                string? qualityScore = null;
                if (item.TryGetProperty("quality_score", out var qsProp))
                {
                    if (qsProp.ValueKind == JsonValueKind.Object && qsProp.TryGetProperty("score", out var scoreProp))
                    {
                        qualityScore = scoreProp.GetString();
                    }
                    else if (qsProp.ValueKind == JsonValueKind.String)
                    {
                        qualityScore = qsProp.GetString();
                    }
                }

                string? headerType = "NONE";
                string? headerText = null;
                string? bodyText = null;
                string? footerText = null;
                string? buttonsJson = null;

                if (item.TryGetProperty("components", out var comps))
                {
                    foreach (var c in comps.EnumerateArray())
                    {
                        var compType = c.GetProperty("type").GetString()?.ToUpperInvariant();
                        if (compType == "HEADER")
                        {
                            headerType = c.TryGetProperty("format", out var f) ? f.GetString() : "TEXT";
                            if (headerType == "TEXT" && c.TryGetProperty("text", out var ht)) headerText = ht.GetString();
                        }
                        else if (compType == "BODY")
                        {
                            if (c.TryGetProperty("text", out var bt)) bodyText = bt.GetString();
                        }
                        else if (compType == "FOOTER")
                        {
                            if (c.TryGetProperty("text", out var ft)) footerText = ft.GetString();
                        }
                        else if (compType == "BUTTONS")
                        {
                            buttonsJson = c.GetRawText();
                        }
                    }
                }

                var match = existingTemplates.FirstOrDefault(t =>
                    (!string.IsNullOrEmpty(metaId) && t.MetaTemplateId == metaId) ||
                    (t.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                     (string.Equals(t.LanguageCode, language, StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(t.Language, language, StringComparison.OrdinalIgnoreCase) ||
                      string.IsNullOrEmpty(t.LanguageCode))));

                if (match == null)
                {
                    match = new CrmWhatsappTemplate
                    {
                        Id = Guid.NewGuid(),
                        Name = name,
                        Language = language,
                        LanguageCode = language,
                        MessageText = bodyText ?? name,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.CrmWhatsappTemplates.Add(match);
                }

                match.MetaTemplateId = metaId;
                match.Category = category;
                match.Status = status;
                match.QualityScore = qualityScore ?? match.QualityScore;
                match.HeaderType = headerType ?? "NONE";
                match.HeaderText = headerText;
                match.BodyText = bodyText;
                match.FooterText = footerText;
                match.ButtonsJson = buttonsJson;
                match.ComponentsJson = comps.GetRawText();
                match.SyncedAt = DateTime.UtcNow;
                match.UpdatedAt = DateTime.UtcNow;

                syncedTemplates.Add(match);
            }

            await _db.SaveChangesAsync(ct);
            return syncedTemplates;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing templates from Meta Graph API");
            return await _db.CrmWhatsappTemplates.ToListAsync(ct);
        }
    }

    public async Task<CrmWhatsappTemplate> CreateTemplateOnMetaAsync(CreateTemplateInput input, CancellationToken ct = default)
    {
        var (wabaId, _, token, _, isSimulated) = await ResolveConfigAsync(ct);

        var template = new CrmWhatsappTemplate
        {
            Id = Guid.NewGuid(),
            Name = input.Name.Trim().ToLowerInvariant().Replace(" ", "_"),
            Category = input.Category,
            Language = input.Language,
            LanguageCode = input.Language,
            HeaderType = input.HeaderType,
            HeaderText = input.HeaderText,
            HeaderMediaUrl = input.HeaderSampleUrl,
            BodyText = input.BodyText,
            FooterText = input.FooterText,
            MessageText = input.BodyText,
            ButtonsJson = input.ButtonsJson,
            Status = isSimulated ? "APPROVED" : "PENDING",
            CreatedAt = DateTime.UtcNow,
            SyncedAt = DateTime.UtcNow
        };

        if (isSimulated || string.IsNullOrEmpty(wabaId) || string.IsNullOrEmpty(token))
        {
            _logger.LogInformation("Template {Name} created in Local/Simulated mode.", template.Name);
            template.MetaTemplateId = $"meta_tpl_{Guid.NewGuid():N}";
            _db.CrmWhatsappTemplates.Add(template);
            await _db.SaveChangesAsync(ct);
            return template;
        }

        // Real Meta Submission
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var components = new List<object>();

            if (input.HeaderType != "NONE")
            {
                var headerObj = new Dictionary<string, object>
                {
                    ["type"] = "HEADER",
                    ["format"] = input.HeaderType
                };
                if (input.HeaderType == "TEXT" && !string.IsNullOrEmpty(input.HeaderText))
                {
                    headerObj["text"] = input.HeaderText;
                }
                else if (input.HeaderType == "IMAGE" || input.HeaderType == "DOCUMENT")
                {
                    if (!string.IsNullOrEmpty(input.HeaderSampleUrl))
                    {
                        headerObj["example"] = new { header_handle = new[] { input.HeaderSampleUrl } };
                    }
                }
                components.Add(headerObj);
            }

            var bodyObj = new Dictionary<string, object>
            {
                ["type"] = "BODY",
                ["text"] = input.BodyText
            };

            if (input.BodySampleValues != null && input.BodySampleValues.Count > 0)
            {
                bodyObj["example"] = new { body_text = new[] { input.BodySampleValues.ToArray() } };
            }
            components.Add(bodyObj);

            if (!string.IsNullOrEmpty(input.FooterText))
            {
                components.Add(new
                {
                    type = "FOOTER",
                    text = input.FooterText
                });
            }

            var payload = new
            {
                name = template.Name,
                category = template.Category,
                language = template.LanguageCode,
                components
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{GraphApiBaseUrl}/{wabaId}/message_templates", jsonContent, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(responseBody);
                template.MetaTemplateId = doc.RootElement.GetProperty("id").GetString();
                template.Status = doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() ?? "PENDING" : "PENDING";
            }
            else
            {
                _logger.LogWarning("Meta template submission rejected: {Resp}", responseBody);
                template.Status = "REJECTED";
                template.RejectedReason = responseBody;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting template to Meta");
            template.Status = "PENDING";
            template.RejectedReason = ex.Message;
        }

        _db.CrmWhatsappTemplates.Add(template);
        await _db.SaveChangesAsync(ct);
        return template;
    }

    public async Task<WabaHealthStatus> GetWabaHealthStatusAsync(CancellationToken ct = default)
    {
        var (wabaId, phoneId, token, _, isSimulated) = await ResolveConfigAsync(ct);

        var today = DateTime.UtcNow.Date;
        var todayUsage = await _db.CrmWhatsappCampaignRecipients
            .CountAsync(r => r.SentAt >= today && r.Status != "Queued", ct);

        if (isSimulated || string.IsNullOrEmpty(phoneId) || string.IsNullOrEmpty(token))
        {
            return new WabaHealthStatus
            {
                IsConnected = true,
                QualityRating = "GREEN",
                MessagingLimitTier = "TIER_10K",
                DisplayPhoneNumber = "+91 98200 12345 (Simulated)",
                VerifiedName = "Tyresoles (India) Pvt Ltd",
                CurrentDayUsage = todayUsage,
                DailyLimit = 10000,
                StatusMessage = "Operating in simulated sandbox mode. Credentials can be configured in WhatsApp Settings."
            };
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var url = $"{GraphApiBaseUrl}/{phoneId}?fields=verified_name,display_phone_number,quality_rating,messaging_limit_tier";
            var response = await client.GetAsync(url, ct);

            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                var qRating = root.TryGetProperty("quality_rating", out var qr) ? qr.GetString() ?? "GREEN" : "GREEN";
                var tier = root.TryGetProperty("messaging_limit_tier", out var mlt) ? mlt.GetString() ?? "TIER_1K" : "TIER_1K";
                var verifiedName = root.TryGetProperty("verified_name", out var vn) ? vn.GetString() : "Tyresoles";
                var dispPhone = root.TryGetProperty("display_phone_number", out var dp) ? dp.GetString() : null;

                int dailyLimit = tier switch
                {
                    "TIER_250" => 250,
                    "TIER_1K" => 1000,
                    "TIER_10K" => 10000,
                    "TIER_100K" => 100000,
                    "TIER_UNLIMITED" => 1000000,
                    _ => 1000
                };

                return new WabaHealthStatus
                {
                    IsConnected = true,
                    QualityRating = qRating.ToUpperInvariant(),
                    MessagingLimitTier = tier,
                    DisplayPhoneNumber = dispPhone,
                    VerifiedName = verifiedName,
                    CurrentDayUsage = todayUsage,
                    DailyLimit = dailyLimit,
                    StatusMessage = "Connected to Meta WhatsApp Cloud API"
                };
            }

            return new WabaHealthStatus
            {
                IsConnected = false,
                QualityRating = "UNKNOWN",
                StatusMessage = $"Failed to authenticate with Meta: HTTP {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            return new WabaHealthStatus
            {
                IsConnected = false,
                QualityRating = "RED",
                StatusMessage = ex.Message
            };
        }
    }

    public bool VerifyWebhookSignature(string rawPayload, string signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader))
            return false;

        var prefix = "sha256=";
        if (!signatureHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var expectedSignature = signatureHeader[prefix.Length..].Trim().ToLowerInvariant();
        var secret = _defaultSettings.AppSecret;

        // Try getting secret from DB settings
        var dbSecret = _db.CrmSettings.FirstOrDefault(s => s.Key == "WHATSAPP_APP_SECRET")?.Value;
        if (!string.IsNullOrWhiteSpace(dbSecret)) secret = dbSecret;

        if (string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogInformation("No WhatsApp App Secret configured in DB or settings. Bypassing signature verification.");
            return true; // allow if no secret configured
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret.Trim()));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawPayload));
        var computedSignature = Convert.ToHexStringLower(hash);

        var match = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedSignature),
            Encoding.UTF8.GetBytes(expectedSignature));

        if (!match)
        {
            _logger.LogWarning("WhatsApp webhook signature mismatch. Computed: {Computed}, Expected: {Expected}", computedSignature, expectedSignature);
        }

        return match;
    }

    public static string NormalizePhoneNumber(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "";
        var digits = Regex.Replace(phone, @"\D", "");
        if (digits.Length == 10)
        {
            return "91" + digits; // Default to India (+91)
        }
        if (digits.Length == 11 && digits.StartsWith("0"))
        {
            return "91" + digits[1..];
        }
        return digits;
    }
}
