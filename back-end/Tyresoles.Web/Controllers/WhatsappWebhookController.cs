using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tyresoles.Data.Features.Crm;
using Tyresoles.Data.Features.Crm.Entities;
using Tyresoles.Web.Services.Whatsapp;

namespace Tyresoles.Web.Controllers;

[ApiController]
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[Route("api/campaigns/webhooks/whatsapp")]
public class WhatsappWebhookController : ControllerBase
{
    private readonly CrmDbContext _db;
    private readonly IWhatsappCloudApiService _whatsappService;
    private readonly WhatsappSettings _settings;
    private readonly ILogger<WhatsappWebhookController> _logger;

    public WhatsappWebhookController(
        CrmDbContext db,
        IWhatsappCloudApiService whatsappService,
        IOptions<WhatsappSettings> settings,
        ILogger<WhatsappWebhookController> logger)
    {
        _db = db;
        _whatsappService = whatsappService;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Meta Webhook Handshake Verification (hub.challenge) or Health Check
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> VerifyWebhook()
    {
        var mode = Request.Query["hub.mode"].ToString();
        var verifyToken = Request.Query["hub.verify_token"].ToString();
        var challenge = Request.Query["hub.challenge"].ToString();

        // Direct browser or uptime monitor probe
        if (string.IsNullOrEmpty(mode) && string.IsNullOrEmpty(verifyToken))
        {
            return Ok(new
            {
                status = "active",
                service = "Tyresoles WhatsApp Cloud API Webhook",
                endpoint = "https://app.tyresoles.in/api/campaigns/webhooks/whatsapp",
                message = "WhatsApp Webhook endpoint is active and listening for Meta events.",
                instructions = "To verify Meta challenge, submit GET with hub.mode=subscribe, hub.verify_token, and hub.challenge parameters.",
                timestamp = DateTime.UtcNow
            });
        }

        var expectedToken = _settings.WebhookVerifyToken;
        var dbToken = await _db.CrmSettings
            .Where(s => s.Key == "WHATSAPP_WEBHOOK_VERIFY_TOKEN")
            .Select(s => s.Value)
            .FirstOrDefaultAsync();
        if (!string.IsNullOrEmpty(dbToken)) expectedToken = dbToken;

        var handshakeLog = new CrmWhatsappWebhookLog
        {
            Id = Guid.NewGuid(),
            EventType = "handshake",
            RawPayload = $"hub.mode={mode}&hub.verify_token={verifyToken}&hub.challenge={challenge}",
            ReceivedAt = DateTime.UtcNow
        };

        if (mode == "subscribe" && verifyToken == expectedToken)
        {
            handshakeLog.ProcessingStatus = "HandshakeSuccess";
            _db.CrmWhatsappWebhookLogs.Add(handshakeLog);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Meta WhatsApp webhook challenge successfully verified.");
            return Content(challenge, "text/plain");
        }

        handshakeLog.ProcessingStatus = "HandshakeFailed";
        handshakeLog.ErrorMessage = $"Received verify_token did not match expected token.";
        _db.CrmWhatsappWebhookLogs.Add(handshakeLog);
        await _db.SaveChangesAsync();

        _logger.LogWarning("Meta WhatsApp webhook verification failed. Received token: {Token}", verifyToken);
        return Forbid();
    }

    /// <summary>
    /// Ping / Connectivity Probe for HEAD and OPTIONS requests
    /// </summary>
    [HttpHead]
    [HttpOptions]
    public IActionResult Ping()
    {
        Response.Headers.Append("Allow", "GET, POST, HEAD, OPTIONS");
        return Ok();
    }

    /// <summary>
    /// Meta Webhook Ingestion for Message Statuses and Inbound Customer Replies
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> HandleWebhookPayload()
    {
        using var reader = new StreamReader(Request.Body);
        var rawJson = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return BadRequest(new { status = "empty", message = "Empty payload." });
        }

        var signatureHeader = Request.Headers["X-Hub-Signature-256"].ToString();

        // 1. Audit log the raw payload immediately
        var auditLog = new CrmWhatsappWebhookLog
        {
            Id = Guid.NewGuid(),
            EventType = "raw",
            ProcessingStatus = "Received",
            RawPayload = rawJson,
            SignatureHeader = signatureHeader,
            ReceivedAt = DateTime.UtcNow
        };
        _db.CrmWhatsappWebhookLogs.Add(auditLog);

        // 2. Validate signature if provided
        if (!string.IsNullOrEmpty(signatureHeader))
        {
            if (!_whatsappService.VerifyWebhookSignature(rawJson, signatureHeader))
            {
                auditLog.ProcessingStatus = "SignatureMismatch";
                auditLog.ErrorMessage = "HMAC SHA-256 signature mismatch against configured App Secret.";
                await _db.SaveChangesAsync();

                _logger.LogWarning("Invalid Meta X-Hub-Signature-256 header. Recorded in webhook log.");
                // Return 200 OK so Meta doesn't disable the webhook subscription while developer investigates
                return Ok(new { status = "signature_mismatch", message = "Recorded in audit log with signature mismatch." });
            }
        }

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("entry", out var entries))
            {
                auditLog.EventType = "unknown";
                auditLog.ProcessingStatus = "Ignored";
                auditLog.ErrorMessage = "No 'entry' array found in payload.";
                await _db.SaveChangesAsync();
                return Ok(new { status = "ignored_no_entry" });
            }

            var processedCount = 0;

            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("changes", out var changes)) continue;

                foreach (var change in changes.EnumerateArray())
                {
                    if (!change.TryGetProperty("value", out var val)) continue;

                    // 1. Outbound Message Delivery Status Updates (sent, delivered, read, failed)
                    if (val.TryGetProperty("statuses", out var statuses))
                    {
                        auditLog.EventType = "statuses";
                        var (statusCount, lastWamid, campaignId, recipientId) = await ProcessStatusUpdatesAsync(statuses);
                        processedCount += statusCount;
                        if (lastWamid != null) auditLog.MetaMessageId = lastWamid;
                        if (campaignId.HasValue) auditLog.CampaignId = campaignId;
                        if (recipientId.HasValue) auditLog.RecipientId = recipientId;
                    }

                    // 2. Inbound Messages / Customer Replies & STOP Opt-outs
                    if (val.TryGetProperty("messages", out var messages))
                    {
                        auditLog.EventType = "messages";
                        var contactsArr = val.TryGetProperty("contacts", out var cArr) ? cArr : default;
                        var (msgCount, fromPhone, lastWamid, campaignId, recipientId) = await ProcessInboundMessagesAsync(messages, contactsArr);
                        processedCount += msgCount;
                        if (fromPhone != null) auditLog.FromPhoneNumber = fromPhone;
                        if (lastWamid != null) auditLog.MetaMessageId = lastWamid;
                        if (campaignId.HasValue) auditLog.CampaignId = campaignId;
                        if (recipientId.HasValue) auditLog.RecipientId = recipientId;
                    }
                }
            }

            auditLog.ProcessingStatus = processedCount > 0 ? "Processed" : "Ignored";
            await _db.SaveChangesAsync();
            return Ok(new { status = "processed", processedCount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing WhatsApp webhook payload.");
            auditLog.ProcessingStatus = "Error";
            auditLog.ErrorMessage = ex.Message + " " + ex.StackTrace;
            await _db.SaveChangesAsync();

            // Always return HTTP 200 to prevent Meta from retrying indefinitely on bad data
            return Ok(new { status = "error_handled", message = ex.Message });
        }
    }

    /// <summary>
    /// Diagnostic & Testing Endpoint: Simulates an incoming Meta webhook event
    /// </summary>
    [HttpPost("test-simulate")]
    public async Task<IActionResult> TestSimulate([FromBody] SimulateWhatsappWebhookRequest req)
    {
        var cleanPhone = WhatsappCloudApiService.NormalizePhoneNumber(req.FromPhoneNumber ?? "919876543210");
        var wamid = string.IsNullOrWhiteSpace(req.MetaMessageId)
            ? $"wamid.simulated_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}"[..Math.Min(60, 50)]
            : req.MetaMessageId;

        var eventType = (req.EventType ?? "messages").ToLowerInvariant();
        var unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        string simulatedJson;
        if (eventType == "messages" || eventType == "text" || eventType == "button" || eventType == "stop")
        {
            var isButton = eventType == "button" || !string.IsNullOrEmpty(req.ButtonPayload);
            var isStop = eventType == "stop" || (req.MessageText ?? "").Trim().Equals("STOP", StringComparison.OrdinalIgnoreCase);

            var messageObj = isButton ? (object)new
            {
                from = cleanPhone,
                id = wamid,
                timestamp = unixTimestamp.ToString(),
                type = "button",
                button = new { text = req.MessageText ?? "Interested", payload = req.ButtonPayload ?? "QUICK_REPLY_YES" }
            } : new
            {
                from = cleanPhone,
                id = wamid,
                timestamp = unixTimestamp.ToString(),
                type = "text",
                text = new { body = isStop ? "STOP" : (req.MessageText ?? "Hi, I am interested in tyre retreading.") }
            };

            var payload = new
            {
                @object = "whatsapp_business_account",
                entry = new[]
                {
                    new
                    {
                        id = "WABA_SIMULATED_ID",
                        changes = new[]
                        {
                            new
                            {
                                field = "messages",
                                value = new
                                {
                                    messaging_product = "whatsapp",
                                    metadata = new { display_phone_number = "919876543210", phone_number_id = "PN_SIMULATED" },
                                    contacts = new[] { new { profile = new { name = req.CustomerName ?? "Simulated Tester" }, wa_id = cleanPhone } },
                                    messages = new[] { messageObj }
                                }
                            }
                        }
                    }
                }
            };
            simulatedJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        }
        else
        {
            // Statuses: delivered, read, failed
            var statusStr = eventType switch
            {
                "read" => "read",
                "failed" => "failed",
                _ => "delivered"
            };

            var statusPayload = new
            {
                id = wamid,
                status = statusStr,
                timestamp = unixTimestamp.ToString(),
                recipient_id = cleanPhone,
                errors = statusStr == "failed" ? new[] { new { code = 131026, title = "Message undeliverable" } } : null
            };

            var payload = new
            {
                @object = "whatsapp_business_account",
                entry = new[]
                {
                    new
                    {
                        id = "WABA_SIMULATED_ID",
                        changes = new[]
                        {
                            new
                            {
                                field = "messages",
                                value = new
                                {
                                    messaging_product = "whatsapp",
                                    metadata = new { display_phone_number = "919876543210", phone_number_id = "PN_SIMULATED" },
                                    statuses = new[] { statusPayload }
                                }
                            }
                        }
                    }
                }
            };
            simulatedJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        }

        var log = new CrmWhatsappWebhookLog
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            ProcessingStatus = "Simulated",
            RawPayload = simulatedJson,
            FromPhoneNumber = cleanPhone,
            MetaMessageId = wamid,
            CampaignId = req.CampaignId,
            RecipientId = req.RecipientId,
            ReceivedAt = DateTime.UtcNow
        };
        _db.CrmWhatsappWebhookLogs.Add(log);

        Guid? inboundId = null;

        // Perform simulation updates
        if (eventType == "messages" || eventType == "text" || eventType == "button" || eventType == "stop")
        {
            var isStop = eventType == "stop" || (req.MessageText ?? "").Trim().Equals("STOP", StringComparison.OrdinalIgnoreCase);

            if (isStop)
            {
                var alreadySuppressed = await _db.CrmWhatsappSuppressionLists.AnyAsync(s => s.PhoneNumber == cleanPhone);
                if (!alreadySuppressed)
                {
                    _db.CrmWhatsappSuppressionLists.Add(new CrmWhatsappSuppressionList
                    {
                        Id = Guid.NewGuid(),
                        PhoneNumber = cleanPhone,
                        Reason = "UserReplyStop",
                        Source = "SimulatedWebhook",
                        Notes = "Customer opted out via simulated test",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
            else
            {
                // Find matching recipient
                CrmWhatsappCampaignRecipient? matchingRecipient = null;
                if (req.RecipientId.HasValue)
                {
                    matchingRecipient = await _db.CrmWhatsappCampaignRecipients
                        .Include(r => r.Campaign)
                        .FirstOrDefaultAsync(r => r.Id == req.RecipientId.Value);
                }
                else if (req.CampaignId.HasValue)
                {
                    matchingRecipient = await _db.CrmWhatsappCampaignRecipients
                        .Include(r => r.Campaign)
                        .FirstOrDefaultAsync(r => r.CampaignId == req.CampaignId.Value && r.PhoneNumber == cleanPhone);
                }
                if (matchingRecipient == null)
                {
                    matchingRecipient = await _db.CrmWhatsappCampaignRecipients
                        .Include(r => r.Campaign)
                        .Where(r => r.PhoneNumber == cleanPhone)
                        .OrderByDescending(r => r.CreatedAt)
                        .FirstOrDefaultAsync();
                }

                inboundId = Guid.NewGuid();
                var inbound = new CrmWhatsappInboundMessage
                {
                    Id = inboundId.Value,
                    FromPhoneNumber = cleanPhone,
                    ProfileName = req.CustomerName ?? "Simulated Tester",
                    MetaMessageId = wamid,
                    ContextWamid = matchingRecipient?.MetaMessageId,
                    CampaignId = req.CampaignId ?? matchingRecipient?.CampaignId,
                    ContactId = matchingRecipient?.ContactId,
                    MessageType = string.IsNullOrEmpty(req.ButtonPayload) ? "text" : "button",
                    MessageBody = req.MessageText ?? "Hi, I am interested in tyre retreading.",
                    ButtonPayload = req.ButtonPayload,
                    FollowupStatus = "Pending",
                    ReceivedAt = DateTime.UtcNow
                };
                _db.CrmWhatsappInboundMessages.Add(inbound);

                if (matchingRecipient != null)
                {
                    matchingRecipient.Status = "Replied";
                    matchingRecipient.RepliedAt = DateTime.UtcNow;
                    matchingRecipient.ReplyMessageText = req.MessageText;
                    if (matchingRecipient.Campaign != null)
                    {
                        matchingRecipient.Campaign.RepliedCount++;
                    }
                    log.CampaignId = matchingRecipient.CampaignId;
                    log.RecipientId = matchingRecipient.Id;
                }
            }
        }
        else
        {
            // Statuses: delivered, read, failed
            CrmWhatsappCampaignRecipient? matchingRecipient = null;
            if (req.RecipientId.HasValue)
            {
                matchingRecipient = await _db.CrmWhatsappCampaignRecipients
                    .Include(r => r.Campaign)
                    .FirstOrDefaultAsync(r => r.Id == req.RecipientId.Value);
            }
            else if (!string.IsNullOrEmpty(req.MetaMessageId))
            {
                matchingRecipient = await _db.CrmWhatsappCampaignRecipients
                    .Include(r => r.Campaign)
                    .FirstOrDefaultAsync(r => r.MetaMessageId == req.MetaMessageId);
            }
            else if (req.CampaignId.HasValue)
            {
                matchingRecipient = await _db.CrmWhatsappCampaignRecipients
                    .Include(r => r.Campaign)
                    .FirstOrDefaultAsync(r => r.CampaignId == req.CampaignId.Value && r.PhoneNumber == cleanPhone);
            }

            if (matchingRecipient != null)
            {
                var now = DateTime.UtcNow;
                if (eventType == "read")
                {
                    matchingRecipient.Status = "Read";
                    matchingRecipient.ReadAt = now;
                    if (matchingRecipient.Campaign != null) matchingRecipient.Campaign.ReadCount++;
                }
                else if (eventType == "failed")
                {
                    matchingRecipient.Status = "Failed";
                    matchingRecipient.ErrorCode = 131026;
                    matchingRecipient.ErrorMessage = "Simulated delivery failure";
                    if (matchingRecipient.Campaign != null) matchingRecipient.Campaign.FailedCount++;
                }
                else
                {
                    matchingRecipient.Status = "Delivered";
                    matchingRecipient.DeliveredAt = now;
                    if (matchingRecipient.Campaign != null) matchingRecipient.Campaign.DeliveredCount++;
                }

                log.CampaignId = matchingRecipient.CampaignId;
                log.RecipientId = matchingRecipient.Id;
            }
        }

        await _db.SaveChangesAsync();

        return Ok(new SimulateWhatsappWebhookResponse
        {
            Success = true,
            Message = $"Simulated '{eventType}' webhook event processed and logged successfully.",
            LogId = log.Id,
            InboundMessageId = inboundId,
            MetaMessageId = wamid
        });
    }

    private async Task<(int count, string? lastWamid, Guid? campaignId, Guid? recipientId)> ProcessStatusUpdatesAsync(JsonElement statuses)
    {
        var count = 0;
        string? lastWamid = null;
        Guid? matchedCampaignId = null;
        Guid? matchedRecipientId = null;

        foreach (var statusObj in statuses.EnumerateArray())
        {
            var wamid = statusObj.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
            var statusStr = statusObj.TryGetProperty("status", out var stProp) ? stProp.GetString()?.ToLowerInvariant() : null;
            if (string.IsNullOrEmpty(wamid) || string.IsNullOrEmpty(statusStr)) continue;

            lastWamid = wamid;
            count++;

            var recipient = await _db.CrmWhatsappCampaignRecipients
                .Include(r => r.Campaign)
                .FirstOrDefaultAsync(r => r.MetaMessageId == wamid);

            if (recipient == null) continue;

            matchedCampaignId = recipient.CampaignId;
            matchedRecipientId = recipient.Id;

            var timestamp = DateTime.UtcNow;
            if (statusObj.TryGetProperty("timestamp", out var tsProp) && long.TryParse(tsProp.GetString(), out var unixSeconds))
            {
                timestamp = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
            }

            if (statusStr == "delivered")
            {
                if (recipient.Status != "Delivered" && recipient.Status != "Read" && recipient.Status != "Replied")
                {
                    recipient.Status = "Delivered";
                    recipient.DeliveredAt = timestamp;
                    if (recipient.Campaign != null) recipient.Campaign.DeliveredCount++;
                }
            }
            else if (statusStr == "read")
            {
                if (recipient.Status != "Read" && recipient.Status != "Replied")
                {
                    if (recipient.Status != "Delivered")
                    {
                        recipient.DeliveredAt ??= timestamp;
                        if (recipient.Campaign != null) recipient.Campaign.DeliveredCount++;
                    }

                    recipient.Status = "Read";
                    recipient.ReadAt = timestamp;
                    if (recipient.Campaign != null) recipient.Campaign.ReadCount++;
                }
            }
            else if (statusStr == "failed")
            {
                recipient.Status = "Failed";
                if (statusObj.TryGetProperty("errors", out var errs) && errs.GetArrayLength() > 0)
                {
                    var firstErr = errs[0];
                    recipient.ErrorCode = firstErr.TryGetProperty("code", out var c) ? c.GetInt32() : 500;
                    recipient.ErrorMessage = firstErr.TryGetProperty("title", out var t) ? t.GetString() : "Message delivery failed";
                }
                if (recipient.Campaign != null) recipient.Campaign.FailedCount++;
            }
        }

        return (count, lastWamid, matchedCampaignId, matchedRecipientId);
    }

    private async Task<(int count, string? fromPhone, string? lastWamid, Guid? campaignId, Guid? recipientId)> ProcessInboundMessagesAsync(JsonElement messages, JsonElement contactsArr)
    {
        var count = 0;
        string? matchedPhone = null;
        string? lastWamid = null;
        Guid? matchedCampaignId = null;
        Guid? matchedRecipientId = null;

        string? profileName = null;
        if (contactsArr.ValueKind == JsonValueKind.Array && contactsArr.GetArrayLength() > 0)
        {
            var contactObj = contactsArr[0];
            if (contactObj.TryGetProperty("profile", out var prof) && prof.TryGetProperty("name", out var n))
            {
                profileName = n.GetString();
            }
        }

        foreach (var msgObj in messages.EnumerateArray())
        {
            var fromPhone = msgObj.TryGetProperty("from", out var f) ? f.GetString() : null;
            var wamid = msgObj.TryGetProperty("id", out var id) ? id.GetString() : null;
            var type = msgObj.TryGetProperty("type", out var t) ? t.GetString() ?? "text" : "text";

            if (string.IsNullOrEmpty(fromPhone) || string.IsNullOrEmpty(wamid)) continue;

            count++;
            lastWamid = wamid;

            string? messageBody = null;
            string? buttonPayload = null;

            if (type == "text" && msgObj.TryGetProperty("text", out var txtObj))
            {
                messageBody = txtObj.TryGetProperty("body", out var b) ? b.GetString() : null;
            }
            else if (type == "button" && msgObj.TryGetProperty("button", out var btnObj))
            {
                messageBody = btnObj.TryGetProperty("text", out var bt) ? bt.GetString() : null;
                buttonPayload = btnObj.TryGetProperty("payload", out var bp) ? bp.GetString() : null;
            }
            else if (type == "interactive" && msgObj.TryGetProperty("interactive", out var intObj))
            {
                if (intObj.TryGetProperty("button_reply", out var br))
                {
                    messageBody = br.TryGetProperty("title", out var brt) ? brt.GetString() : null;
                    buttonPayload = br.TryGetProperty("id", out var brid) ? brid.GetString() : null;
                }
            }

            string? contextWamid = null;
            if (msgObj.TryGetProperty("context", out var ctx) && ctx.TryGetProperty("id", out var cid))
            {
                contextWamid = cid.GetString();
            }

            var cleanFromPhone = WhatsappCloudApiService.NormalizePhoneNumber(fromPhone);
            matchedPhone = cleanFromPhone;

            // 1. Check if STOP / Opt-Out request
            var textToCheck = (messageBody ?? buttonPayload ?? "").Trim().ToUpperInvariant();
            if (textToCheck == "STOP" || textToCheck == "UNSUBSCRIBE" || textToCheck == "OPT_OUT" || textToCheck == "EXIT")
            {
                _logger.LogInformation("Customer {Phone} requested WhatsApp unsubscribe via STOP keyword.", cleanFromPhone);
                var alreadySuppressed = await _db.CrmWhatsappSuppressionLists
                    .AnyAsync(s => s.PhoneNumber == cleanFromPhone);

                if (!alreadySuppressed)
                {
                    _db.CrmWhatsappSuppressionLists.Add(new CrmWhatsappSuppressionList
                    {
                        Id = Guid.NewGuid(),
                        PhoneNumber = cleanFromPhone,
                        Reason = "UserReplyStop",
                        Source = "Webhook",
                        Notes = $"Customer replied: '{messageBody}'",
                        CreatedAt = DateTime.UtcNow
                    });
                }

                // If associated with a campaign recipient, mark Suppressed
                var rep = await _db.CrmWhatsappCampaignRecipients
                    .OrderByDescending(r => r.CreatedAt)
                    .FirstOrDefaultAsync(r => r.PhoneNumber == cleanFromPhone);
                if (rep != null)
                {
                    rep.Status = "Suppressed";
                    matchedCampaignId = rep.CampaignId;
                    matchedRecipientId = rep.Id;
                }
                continue;
            }

            // 2. Regular Customer Inbound Reply
            CrmWhatsappCampaignRecipient? matchingRecipient = null;
            if (!string.IsNullOrEmpty(contextWamid))
            {
                matchingRecipient = await _db.CrmWhatsappCampaignRecipients
                    .Include(r => r.Campaign)
                    .FirstOrDefaultAsync(r => r.MetaMessageId == contextWamid);
            }
            if (matchingRecipient == null)
            {
                matchingRecipient = await _db.CrmWhatsappCampaignRecipients
                    .Include(r => r.Campaign)
                    .Where(r => r.PhoneNumber == cleanFromPhone)
                    .OrderByDescending(r => r.CreatedAt)
                    .FirstOrDefaultAsync();
            }

            if (matchingRecipient != null)
            {
                matchedCampaignId = matchingRecipient.CampaignId;
                matchedRecipientId = matchingRecipient.Id;
            }

            var inbound = new CrmWhatsappInboundMessage
            {
                Id = Guid.NewGuid(),
                FromPhoneNumber = cleanFromPhone,
                ProfileName = profileName,
                MetaMessageId = wamid,
                ContextWamid = contextWamid,
                CampaignId = matchingRecipient?.CampaignId,
                ContactId = matchingRecipient?.ContactId,
                MessageType = type,
                MessageBody = messageBody,
                ButtonPayload = buttonPayload,
                FollowupStatus = "Pending",
                ReceivedAt = DateTime.UtcNow
            };
            _db.CrmWhatsappInboundMessages.Add(inbound);

            if (matchingRecipient != null)
            {
                matchingRecipient.Status = "Replied";
                matchingRecipient.RepliedAt = DateTime.UtcNow;
                matchingRecipient.ReplyMessageText = messageBody;
                if (matchingRecipient.Campaign != null)
                {
                    matchingRecipient.Campaign.RepliedCount++;
                }
            }
        }

        return (count, matchedPhone, lastWamid, matchedCampaignId, matchedRecipientId);
    }
}
