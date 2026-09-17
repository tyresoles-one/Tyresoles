using System;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Tyresoles.Data.Features.Crm;
using Tyresoles.Data.Features.Crm.Entities;

namespace Tyresoles.Web.Controllers;

[ApiController]
[Route("api/campaigns")]
public class EmailTrackingController : ControllerBase
{
    private readonly CrmDbContext _db;
    private readonly ILogger<EmailTrackingController> _logger;

    // 1x1 transparent PNG binary bytes
    private static readonly byte[] TransparentPixelBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");

    public EmailTrackingController(CrmDbContext db, ILogger<EmailTrackingController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet("track/open/{token}.png")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> TrackOpen(string token)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(token))
            {
                var recipient = await _db.CrmEmailCampaignRecipients
                    .Include(r => r.Campaign)
                    .FirstOrDefaultAsync(r => r.TrackingToken == token);

                if (recipient != null)
                {
                    var userAgent = Request.Headers.UserAgent.ToString();
                    var isMachine = userAgent.Contains("GoogleImageProxy", StringComparison.OrdinalIgnoreCase) ||
                                    userAgent.Contains("Apple-Mail-Access", StringComparison.OrdinalIgnoreCase) ||
                                    userAgent.Contains("bingbot", StringComparison.OrdinalIgnoreCase);

                    var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

                    // Log event
                    var eventLog = new CrmEmailEventLog
                    {
                        Id = Guid.NewGuid(),
                        CampaignId = recipient.CampaignId,
                        RecipientId = recipient.Id,
                        EmailAddress = recipient.EmailAddress,
                        EventType = "Open",
                        UserAgent = userAgent,
                        IpAddress = ip,
                        IsMachineOpen = isMachine,
                        Timestamp = DateTime.UtcNow
                    };
                    _db.CrmEmailEventLogs.Add(eventLog);

                    if (recipient.OpenCount == 0)
                    {
                        if (recipient.Campaign != null)
                        {
                            recipient.Campaign.UniqueOpenedCount++;
                        }
                    }

                    recipient.OpenCount++;
                    recipient.OpenedAt ??= DateTime.UtcNow;
                    if (recipient.Status == "Sent" || recipient.Status == "Delivered")
                    {
                        recipient.Status = "Opened";
                    }

                    if (recipient.Campaign != null)
                    {
                        recipient.Campaign.OpenedCount++;
                    }

                    await _db.SaveChangesAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing open tracking for token {Token}", token);
        }

        return File(TransparentPixelBytes, "image/png");
    }

    [HttpGet("track/click/{token}")]
    public async Task<IActionResult> TrackClick(string token, [FromQuery] string? target)
    {
        var destinationUrl = "https://tyresoles.in";

        try
        {
            if (!string.IsNullOrWhiteSpace(token))
            {
                var recipient = await _db.CrmEmailCampaignRecipients
                    .Include(r => r.Campaign)
                    .FirstOrDefaultAsync(r => r.TrackingToken == token);

                if (recipient != null)
                {
                    if (!string.IsNullOrWhiteSpace(target))
                    {
                        destinationUrl = target;
                    }

                    // Append UTM parameters if not already present
                    if (!destinationUrl.Contains("utm_source=", StringComparison.OrdinalIgnoreCase))
                    {
                        var separator = destinationUrl.Contains('?') ? "&" : "?";
                        var campaignSlug = recipient.Campaign != null ? UrlEncoder.Default.Encode(recipient.Campaign.Name.Replace(" ", "_").ToLower()) : "email";
                        destinationUrl = $"{destinationUrl}{separator}utm_source=tyresoles_crm&utm_medium=email&utm_campaign={campaignSlug}";
                    }

                    var userAgent = Request.Headers.UserAgent.ToString();
                    var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

                    var eventLog = new CrmEmailEventLog
                    {
                        Id = Guid.NewGuid(),
                        CampaignId = recipient.CampaignId,
                        RecipientId = recipient.Id,
                        EmailAddress = recipient.EmailAddress,
                        EventType = "Click",
                        Details = destinationUrl,
                        UserAgent = userAgent,
                        IpAddress = ip,
                        Timestamp = DateTime.UtcNow
                    };
                    _db.CrmEmailEventLogs.Add(eventLog);

                    if (recipient.ClickCount == 0)
                    {
                        if (recipient.Campaign != null)
                        {
                            recipient.Campaign.UniqueClickedCount++;
                        }
                    }

                    recipient.ClickCount++;
                    recipient.ClickedAt ??= DateTime.UtcNow;
                    recipient.Status = "Clicked";

                    if (recipient.Campaign != null)
                    {
                        recipient.Campaign.ClickedCount++;
                    }

                    await _db.SaveChangesAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing click tracking for token {Token}", token);
        }

        return Redirect(destinationUrl);
    }

    [HttpPost("unsubscribe")]
    public async Task<IActionResult> HandleOneClickUnsubscribe([FromQuery] string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new { error = "Token is required." });
        }

        var recipient = await _db.CrmEmailCampaignRecipients
            .Include(r => r.Campaign)
            .FirstOrDefaultAsync(r => r.TrackingToken == token);

        if (recipient == null)
        {
            return NotFound(new { error = "Subscription record not found." });
        }

        await ExecuteUnsubscribeAsync(recipient, "RFC 8058 One-Click Header");
        return Ok(new { status = "unsubscribed", email = recipient.EmailAddress });
    }

    [HttpGet("unsubscribe")]
    public async Task<IActionResult> HandleUnsubscribePage([FromQuery] string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Content(RenderUnsubscribeHtml("Invalid or missing unsubscribe token.", false), "text/html");
        }

        var recipient = await _db.CrmEmailCampaignRecipients
            .Include(r => r.Campaign)
            .FirstOrDefaultAsync(r => r.TrackingToken == token);

        if (recipient == null)
        {
            return Content(RenderUnsubscribeHtml("Email address could not be found or was already removed.", false), "text/html");
        }

        await ExecuteUnsubscribeAsync(recipient, "User clicked in-email unsubscribe link");
        return Content(RenderUnsubscribeHtml($"You have been successfully unsubscribed: {recipient.EmailAddress}", true), "text/html");
    }

    private async Task ExecuteUnsubscribeAsync(CrmEmailCampaignRecipient recipient, string source)
    {
        var cleanEmail = recipient.EmailAddress.Trim().ToLowerInvariant();

        // Add to permanent suppression list if not already present
        var existingSuppression = await _db.CrmEmailSuppressionLists
            .FirstOrDefaultAsync(s => s.EmailAddress.ToLower() == cleanEmail);

        if (existingSuppression == null)
        {
            _db.CrmEmailSuppressionLists.Add(new CrmEmailSuppressionList
            {
                Id = Guid.NewGuid(),
                EmailAddress = recipient.EmailAddress,
                Reason = "Unsubscribe",
                DiagnosticCode = source,
                SourceCampaignId = recipient.CampaignId,
                CreatedAt = DateTime.UtcNow
            });
        }

        recipient.Status = "Unsubscribed";
        if (recipient.Campaign != null)
        {
            recipient.Campaign.UnsubscribedCount++;
        }

        _db.CrmEmailEventLogs.Add(new CrmEmailEventLog
        {
            Id = Guid.NewGuid(),
            CampaignId = recipient.CampaignId,
            RecipientId = recipient.Id,
            EmailAddress = recipient.EmailAddress,
            EventType = "Unsubscribe",
            Details = source,
            Timestamp = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    private static string RenderUnsubscribeHtml(string message, bool isSuccess)
    {
        var color = isSuccess ? "#16a34a" : "#dc2626";
        var icon = isSuccess ? "✓" : "!";

        return $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Unsubscribe - Tyresoles</title>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background-color: #f8fafc; margin: 0; padding: 40px 20px; display: flex; align-items: center; justify-content: center; min-height: 80vh; }}
        .card {{ background: white; border-radius: 12px; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1), 0 2px 4px -2px rgba(0,0,0,0.1); max-width: 480px; width: 100%; padding: 32px; text-align: center; }}
        .badge {{ width: 56px; height: 56px; line-height: 56px; border-radius: 50%; background: {color}15; color: {color}; font-size: 28px; font-weight: bold; margin: 0 auto 20px; }}
        h1 {{ font-size: 20px; color: #1e293b; margin: 0 0 12px; }}
        p {{ color: #64748b; font-size: 15px; line-height: 1.5; margin: 0 0 24px; }}
        .footer {{ font-size: 13px; color: #94a3b8; border-top: 1px solid #e2e8f0; padding-top: 16px; margin-top: 24px; }}
    </style>
</head>
<body>
    <div class=""card"">
        <div class=""badge"">{icon}</div>
        <h1>Email Preferences Updated</h1>
        <p>{message}</p>
        <p>You will no longer receive marketing emails from Tyresoles at this address.</p>
        <div class=""footer"">
            Tyresoles (India) Pvt. Ltd. &bull; tyresoles.in
        </div>
    </div>
</body>
</html>";
    }
}
