using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tyresoles.Data.Features.Crm;
using Tyresoles.Data.Features.Crm.Entities;

namespace Tyresoles.Web.Services.Email;

public class EmailCampaignBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AmazonSesSmtpSettings _settings;
    private readonly ILogger<EmailCampaignBackgroundService> _logger;

    public EmailCampaignBackgroundService(
        IServiceProvider serviceProvider,
        IOptions<AmazonSesSmtpSettings> settings,
        ILogger<EmailCampaignBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EmailCampaignBackgroundService is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingCampaignsAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error occurred during email campaign dispatch loop.");
            }

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }

        _logger.LogInformation("EmailCampaignBackgroundService has stopped.");
    }

    private async Task ProcessPendingCampaignsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        var senderService = scope.ServiceProvider.GetRequiredService<IEmailCampaignSenderService>();

        var now = DateTime.UtcNow;

        // 1. Activate any scheduled campaigns whose time has arrived
        var readyCampaigns = await db.CrmEmailCampaigns
            .Where(c => c.Status == "Scheduled" && (c.ScheduledAt == null || c.ScheduledAt <= now))
            .ToListAsync(cancellationToken);

        foreach (var c in readyCampaigns)
        {
            c.Status = "InProgress";
            c.StartedAt ??= now;
            c.UpdatedAt = now;
        }

        if (readyCampaigns.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        // 2. Fetch the next InProgress campaign to dispatch
        var activeCampaign = await db.CrmEmailCampaigns
            .Where(c => c.Status == "InProgress")
            .OrderBy(c => c.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeCampaign == null) return;

        // Fetch queued recipients batch
        var batchSize = Math.Max(5, _settings.MaxSendRatePerSecond * 2);
        var queuedRecipients = await db.CrmEmailCampaignRecipients
            .Include(r => r.Contact)
            .Where(r => r.CampaignId == activeCampaign.Id && r.Status == "Queued")
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (queuedRecipients.Count == 0)
        {
            // All recipients processed for this campaign
            activeCampaign.Status = "Completed";
            activeCampaign.CompletedAt = DateTime.UtcNow;
            activeCampaign.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Campaign {CampaignId} ({Name}) marked Completed.", activeCampaign.Id, activeCampaign.Name);
            return;
        }

        // Fetch all suppression list emails in this batch
        var batchEmails = queuedRecipients.Select(r => r.EmailAddress.Trim().ToLowerInvariant()).Distinct().ToList();
        var suppressedEmails = await db.CrmEmailSuppressionLists
            .Where(s => batchEmails.Contains(s.EmailAddress.ToLower()))
            .Select(s => s.EmailAddress.ToLower())
            .ToListAsync(cancellationToken);

        var suppressedSet = new HashSet<string>(suppressedEmails, StringComparer.OrdinalIgnoreCase);

        var delayBetweenSendsMs = Math.Max(100, 1000 / Math.Max(1, _settings.MaxSendRatePerSecond));
        var random = new Random();

        foreach (var recipient in queuedRecipients)
        {
            if (cancellationToken.IsCancellationRequested) break;

            // Check Suppression List (Zero-Bypass Gatekeeper)
            if (suppressedSet.Contains(recipient.EmailAddress.Trim().ToLowerInvariant()))
            {
                recipient.Status = "Suppressed";
                recipient.ErrorMessage = "Recipient email is on the permanent suppression list (bounced or unsubscribed).";
                continue;
            }

            // Send via SES
            var success = await senderService.SendSingleCampaignEmailAsync(activeCampaign, recipient, recipient.Contact, cancellationToken);
            if (success)
            {
                activeCampaign.SentCount++;
                activeCampaign.DeliveredCount++; // Preliminary delivery status until webhook reports otherwise
            }

            // Enforce rate-limit with small randomized jitter (50ms - 150ms)
            var jitter = random.Next(50, 150);
            await Task.Delay(delayBetweenSendsMs + jitter, cancellationToken);
        }

        activeCampaign.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}
