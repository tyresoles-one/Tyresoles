using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tyresoles.Data.Features.Crm;
using Tyresoles.Data.Features.Crm.Entities;

namespace Tyresoles.Web.Services.Whatsapp;

public class WhatsappCampaignBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly WhatsappSettings _settings;
    private readonly ILogger<WhatsappCampaignBackgroundService> _logger;

    public WhatsappCampaignBackgroundService(
        IServiceProvider serviceProvider,
        IOptions<WhatsappSettings> settings,
        ILogger<WhatsappCampaignBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WhatsappCampaignBackgroundService is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingCampaignsAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error occurred during WhatsApp campaign dispatch loop.");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }

        _logger.LogInformation("WhatsappCampaignBackgroundService has stopped.");
    }

    private async Task ProcessPendingCampaignsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        var whatsappApi = scope.ServiceProvider.GetRequiredService<IWhatsappCloudApiService>();

        var now = DateTime.UtcNow;

        // 1. Activate scheduled campaigns
        var readyCampaigns = await db.CrmWhatsappCampaigns
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

        // 2. Fetch the current InProgress campaign
        var activeCampaign = await db.CrmWhatsappCampaigns
            .Include(c => c.Template)
            .Where(c => c.Status == "InProgress")
            .OrderBy(c => c.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeCampaign == null) return;

        // 3. Fetch a batch of queued recipients
        int batchSize = Math.Max(10, _settings.MaxSendRatePerSecond);
        var queuedRecipients = await db.CrmWhatsappCampaignRecipients
            .Include(r => r.Contact)
            .Where(r => r.CampaignId == activeCampaign.Id && r.Status == "Queued")
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (queuedRecipients.Count == 0)
        {
            // All recipients dispatched
            activeCampaign.Status = "Completed";
            activeCampaign.CompletedAt = DateTime.UtcNow;
            activeCampaign.UpdatedAt = DateTime.UtcNow;
            activeCampaign.ActualCost = activeCampaign.DeliveredCount * activeCampaign.CostPerMessage;
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("WhatsApp Campaign {Id} ({Name}) marked Completed.", activeCampaign.Id, activeCampaign.Name);
            return;
        }

        // Fetch suppression list
        var recipientPhones = queuedRecipients.Select(r => r.PhoneNumber).ToList();
        var suppressedPhones = await db.CrmWhatsappSuppressionLists
            .Where(s => recipientPhones.Contains(s.PhoneNumber))
            .Select(s => s.PhoneNumber)
            .ToListAsync(cancellationToken);
        var suppressionSet = new HashSet<string>(suppressedPhones, StringComparer.OrdinalIgnoreCase);

        // Parse variable mappings
        Dictionary<string, string> variableMappings = new();
        if (!string.IsNullOrEmpty(activeCampaign.VariableMappingsJson))
        {
            try
            {
                variableMappings = JsonSerializer.Deserialize<Dictionary<string, string>>(activeCampaign.VariableMappingsJson) 
                                   ?? new Dictionary<string, string>();
            }
            catch { }
        }

        string templateName = activeCampaign.Template?.Name ?? activeCampaign.TemplateName ?? "general_notice";
        string langCode = activeCampaign.Template?.LanguageCode ?? activeCampaign.LanguageCode ?? "en";

        int failuresInBatch = 0;

        foreach (var r in queuedRecipients)
        {
            if (cancellationToken.IsCancellationRequested) break;

            // Check suppression
            if (suppressionSet.Contains(r.PhoneNumber))
            {
                r.Status = "Suppressed";
                r.ErrorMessage = "Number is on WhatsApp Suppression / Opt-out list.";
                continue;
            }

            // Build components
            var components = new List<WhatsappTemplateComponent>();

            // Header Media if applicable
            var templateHeaderType = activeCampaign.Template?.HeaderType?.ToUpperInvariant() ?? "NONE";
            var hasMediaHeader = templateHeaderType == "IMAGE" || templateHeaderType == "DOCUMENT" || templateHeaderType == "VIDEO"
                || (activeCampaign.Template == null && !string.IsNullOrEmpty(activeCampaign.HeaderMediaUrl));

            if (hasMediaHeader && !string.IsNullOrEmpty(activeCampaign.HeaderMediaUrl))
            {
                var isDoc = templateHeaderType == "DOCUMENT" || activeCampaign.HeaderMediaUrl.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
                var headerComp = new WhatsappTemplateComponent
                {
                    Type = "header",
                    Parameters = new List<WhatsappParameter>
                    {
                        new()
                        {
                            Type = isDoc ? "document" : "image",
                            Image = isDoc ? null : new WhatsappMediaParam { Link = activeCampaign.HeaderMediaUrl },
                            Document = isDoc ? new WhatsappMediaParam { Link = activeCampaign.HeaderMediaUrl, Filename = "Tyresoles_Offer.pdf" } : null
                        }
                    }
                };
                components.Add(headerComp);
            }

            // Body Parameters based on variable mappings
            if (variableMappings.Count > 0)
            {
                var bodyParameters = new List<WhatsappParameter>();
                // Sort keys "1", "2", "3"...
                var sortedKeys = variableMappings.Keys
                    .OrderBy(k => int.TryParse(k.Trim('{', '}'), out var num) ? num : 99)
                    .ToList();

                foreach (var key in sortedKeys)
                {
                    var fieldName = variableMappings[key];
                    var val = ResolveContactValue(r, fieldName);
                    bodyParameters.Add(new WhatsappParameter { Type = "text", Text = val });
                }

                components.Add(new WhatsappTemplateComponent
                {
                    Type = "body",
                    Parameters = bodyParameters
                });
            }

            // Call API
            var result = await whatsappApi.SendTemplateMessageAsync(r.PhoneNumber, templateName, langCode, components, cancellationToken);

            if (result.Success)
            {
                r.Status = "Sent";
                r.MetaMessageId = result.Wamid;
                r.SentAt = DateTime.UtcNow;
                activeCampaign.SentCount++;
            }
            else
            {
                r.Status = "Failed";
                r.ErrorCode = result.ErrorCode;
                r.ErrorMessage = result.ErrorMessage;
                activeCampaign.FailedCount++;
                failuresInBatch++;

                // If Rate Limit Hit (130429), back off
                if (result.ErrorCode == 130429)
                {
                    _logger.LogWarning("Throughput rate limit hit (130429). Pacing delay...");
                    await Task.Delay(2000, cancellationToken);
                }
            }

            // Small delay to respect rate-limit
            await Task.Delay(25, cancellationToken);
        }

        // Safety Circuit-breaker: If batch had 10+ items and all failed, pause campaign
        if (queuedRecipients.Count >= 10 && failuresInBatch == queuedRecipients.Count)
        {
            activeCampaign.Status = "Paused";
            activeCampaign.FailureReason = "Auto-paused: All messages in batch failed to send. Check Meta WABA credentials and template status.";
            _logger.LogWarning("WhatsApp Campaign {Id} paused due to consecutive failure threshold.", activeCampaign.Id);
        }

        activeCampaign.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string ResolveContactValue(CrmWhatsappCampaignRecipient recipient, string fieldName)
    {
        var contact = recipient.Contact;
        return fieldName.ToLowerInvariant() switch
        {
            "fullname" => !string.IsNullOrWhiteSpace(recipient.FullName) ? recipient.FullName : (contact?.FullName ?? "Valued Customer"),
            "companyname" => !string.IsNullOrWhiteSpace(recipient.CompanyName) ? recipient.CompanyName : (contact?.CompanyName ?? "Your Company"),
            "city" => contact?.City ?? "your city",
            "state" => contact?.State ?? "your state",
            "respcenter" => contact?.RespCenter ?? "Tyresoles Depot",
            "products" => contact?.Products ?? "Commercial Fleet Tyres",
            _ => fieldName // Fallback to literal text if custom
        };
    }
}
