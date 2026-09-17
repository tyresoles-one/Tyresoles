using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Tyresoles.Data.Features.Crm;
using Tyresoles.Data.Features.Crm.Entities;

namespace Tyresoles.Web.Controllers;

[ApiController]
[Route("api/campaigns/webhooks")]
public class SesWebhookController : ControllerBase
{
    private readonly CrmDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SesWebhookController> _logger;

    public SesWebhookController(
        CrmDbContext db,
        IHttpClientFactory httpClientFactory,
        ILogger<SesWebhookController> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpPost("ses")]
    public async Task<IActionResult> HandleSesNotification()
    {
        using var reader = new StreamReader(Request.Body);
        var rawJson = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return BadRequest("Empty payload.");
        }

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            // Check SNS message type
            var msgType = root.TryGetProperty("Type", out var typeProp) ? typeProp.GetString() : null;

            // 1. Automatic Subscription Confirmation Handshake
            if (msgType == "SubscriptionConfirmation")
            {
                if (root.TryGetProperty("SubscribeURL", out var subUrlProp))
                {
                    var subscribeUrl = subUrlProp.GetString();
                    if (!string.IsNullOrEmpty(subscribeUrl))
                    {
                        var client = _httpClientFactory.CreateClient();
                        var response = await client.GetAsync(subscribeUrl);
                        _logger.LogInformation("Confirmed Amazon SNS HTTP subscription. Status: {StatusCode}", response.StatusCode);
                        return Ok(new { status = "subscription_confirmed" });
                    }
                }
            }

            // 2. Notification Processing
            if (msgType == "Notification" && root.TryGetProperty("Message", out var messageProp))
            {
                var messageContent = messageProp.GetString();
                if (!string.IsNullOrWhiteSpace(messageContent))
                {
                    await ProcessSesMessageAsync(messageContent);
                }
            }

            return Ok(new { status = "processed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse or process SES webhook notification.");
            return StatusCode(500, "Internal error processing webhook.");
        }
    }

    private async Task ProcessSesMessageAsync(string messageJson)
    {
        using var msgDoc = JsonDocument.Parse(messageJson);
        var root = msgDoc.RootElement;

        var notificationType = root.TryGetProperty("eventType", out var eventTypeProp)
            ? eventTypeProp.GetString()
            : (root.TryGetProperty("notificationType", out var notifTypeProp) ? notifTypeProp.GetString() : null);

        var mailElement = root.TryGetProperty("mail", out var mailProp) ? mailProp : default;
        var messageId = mailElement.ValueKind != JsonValueKind.Undefined && mailElement.TryGetProperty("messageId", out var idProp)
            ? idProp.GetString()
            : null;

        // Process BOUNCES
        if (string.Equals(notificationType, "Bounce", StringComparison.OrdinalIgnoreCase) && root.TryGetProperty("bounce", out var bounceElement))
        {
            var bounceType = bounceElement.TryGetProperty("bounceType", out var bt) ? bt.GetString() : "Undetermined";
            var bounceSubType = bounceElement.TryGetProperty("bounceSubType", out var bst) ? bst.GetString() : "";

            if (bounceElement.TryGetProperty("bouncedRecipients", out var bouncedList) && bouncedList.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in bouncedList.EnumerateArray())
                {
                    var email = item.TryGetProperty("emailAddress", out var emailProp) ? emailProp.GetString() : null;
                    var diagCode = item.TryGetProperty("diagnosticCode", out var diagProp) ? diagProp.GetString() : $"{bounceType} - {bounceSubType}";

                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        var cleanEmail = email.Trim().ToLowerInvariant();

                        // Suppress if permanent or repeated
                        if (string.Equals(bounceType, "Permanent", StringComparison.OrdinalIgnoreCase))
                        {
                            var existing = await _db.CrmEmailSuppressionLists.FirstOrDefaultAsync(s => s.EmailAddress.ToLower() == cleanEmail);
                            if (existing == null)
                            {
                                _db.CrmEmailSuppressionLists.Add(new CrmEmailSuppressionList
                                {
                                    Id = Guid.NewGuid(),
                                    EmailAddress = email,
                                    Reason = "HardBounce",
                                    DiagnosticCode = diagCode,
                                    CreatedAt = DateTime.UtcNow
                                });
                            }
                        }

                        // Update Recipient record
                        var recipient = await _db.CrmEmailCampaignRecipients
                            .Include(r => r.Campaign)
                            .Where(r => r.EmailAddress.ToLower() == cleanEmail)
                            .OrderByDescending(r => r.CreatedAt)
                            .FirstOrDefaultAsync();

                        if (recipient != null)
                        {
                            recipient.Status = "Bounced";
                            recipient.BouncedAt = DateTime.UtcNow;
                            recipient.BounceType = bounceType;
                            recipient.BounceReason = diagCode;

                            if (recipient.Campaign != null)
                            {
                                recipient.Campaign.BouncedCount++;
                            }

                            _db.CrmEmailEventLogs.Add(new CrmEmailEventLog
                            {
                                Id = Guid.NewGuid(),
                                CampaignId = recipient.CampaignId,
                                RecipientId = recipient.Id,
                                EmailAddress = email,
                                EventType = "Bounce",
                                Details = diagCode,
                                Timestamp = DateTime.UtcNow
                            });
                        }
                    }
                }
                await _db.SaveChangesAsync();
            }
        }
        // Process COMPLAINTS (Spam Reports)
        else if (string.Equals(notificationType, "Complaint", StringComparison.OrdinalIgnoreCase) && root.TryGetProperty("complaint", out var complaintElement))
        {
            if (complaintElement.TryGetProperty("complainedRecipients", out var complainedList) && complainedList.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in complainedList.EnumerateArray())
                {
                    var email = item.TryGetProperty("emailAddress", out var emailProp) ? emailProp.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        var cleanEmail = email.Trim().ToLowerInvariant();
                        var existing = await _db.CrmEmailSuppressionLists.FirstOrDefaultAsync(s => s.EmailAddress.ToLower() == cleanEmail);
                        if (existing == null)
                        {
                            _db.CrmEmailSuppressionLists.Add(new CrmEmailSuppressionList
                            {
                                Id = Guid.NewGuid(),
                                EmailAddress = email,
                                Reason = "SpamComplaint",
                                DiagnosticCode = "Feedback Loop Complaint",
                                CreatedAt = DateTime.UtcNow
                            });
                        }

                        var recipient = await _db.CrmEmailCampaignRecipients
                            .Include(r => r.Campaign)
                            .Where(r => r.EmailAddress.ToLower() == cleanEmail)
                            .OrderByDescending(r => r.CreatedAt)
                            .FirstOrDefaultAsync();

                        if (recipient != null)
                        {
                            recipient.Status = "Complaint";
                            if (recipient.Campaign != null)
                            {
                                recipient.Campaign.SpamComplaintCount++;
                            }

                            _db.CrmEmailEventLogs.Add(new CrmEmailEventLog
                            {
                                Id = Guid.NewGuid(),
                                CampaignId = recipient.CampaignId,
                                RecipientId = recipient.Id,
                                EmailAddress = email,
                                EventType = "Complaint",
                                Details = "Recipient marked email as spam",
                                Timestamp = DateTime.UtcNow
                            });
                        }
                    }
                }
                await _db.SaveChangesAsync();
            }
        }
        // Process DELIVERIES
        else if (string.Equals(notificationType, "Delivery", StringComparison.OrdinalIgnoreCase) && root.TryGetProperty("delivery", out var deliveryElement))
        {
            if (deliveryElement.TryGetProperty("recipients", out var recipientsList) && recipientsList.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in recipientsList.EnumerateArray())
                {
                    var email = item.GetString();
                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        var cleanEmail = email.Trim().ToLowerInvariant();
                        var recipient = await _db.CrmEmailCampaignRecipients
                            .Where(r => r.EmailAddress.ToLower() == cleanEmail && r.Status == "Sent")
                            .OrderByDescending(r => r.CreatedAt)
                            .FirstOrDefaultAsync();

                        if (recipient != null)
                        {
                            recipient.Status = "Delivered";
                            recipient.DeliveredAt = DateTime.UtcNow;
                        }
                    }
                }
                await _db.SaveChangesAsync();
            }
        }
    }
}
