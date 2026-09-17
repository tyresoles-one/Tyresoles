using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HotChocolate;
using HotChocolate.Types;
using Microsoft.EntityFrameworkCore;
using Tyresoles.Data.Features.Crm;
using Tyresoles.Data.Features.Crm.Entities;
using Tyresoles.Web.Services.Email;

namespace Tyresoles.Web.GraphQL;

public class AudienceFilterInput
{
    public string? ContactCategory { get; set; }
    public string? State { get; set; }
    public string? City { get; set; }
    public string? RespCenter { get; set; }
    public string? Tag { get; set; }
    public decimal? MinQualityScore { get; set; }
    public bool OnlyWithEmail { get; set; } = true;
}

public class AudienceEstimateResult
{
    public int TotalMatchingContacts { get; set; }
    public int WithValidEmail { get; set; }
    public int SuppressedCount { get; set; }
    public int EligibleRecipients { get; set; }
}

public class SaveCrmEmailCampaignInput
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? PreviewText { get; set; }
    public string FromName { get; set; } = "Tyresoles Fleet Team";
    public string FromEmail { get; set; } = "updates@tyresoles.in";
    public string? ReplyToEmail { get; set; }
    public string CampaignType { get; set; } = "Broadcast";
    public string ContentType { get; set; } = "Html";
    public string? BodyHtml { get; set; }
    public string? BodyText { get; set; }
    public AudienceFilterInput? AudienceFilter { get; set; }
}

public class SaveCrmEmailTemplateInput
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string Subject { get; set; } = string.Empty;
    public string? PreviewText { get; set; }
    public string? BodyHtml { get; set; }
    public string? BodyText { get; set; }
}

[ExtendObjectType(typeof(Query))]
public class CrmCampaignQueryExtension
{
    [GraphQLName("getCrmEmailCampaigns")]
    public async Task<List<CrmEmailCampaign>> GetCrmEmailCampaigns(
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        return await db.CrmEmailCampaigns
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    [GraphQLName("getCrmEmailCampaignDetails")]
    public async Task<CrmEmailCampaign?> GetCrmEmailCampaignDetails(
        Guid id,
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        return await db.CrmEmailCampaigns
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    [GraphQLName("getCrmEmailCampaignRecipients")]
    public async Task<List<CrmEmailCampaignRecipient>> GetCrmEmailCampaignRecipients(
        Guid campaignId,
        int skip,
        int take,
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        return await db.CrmEmailCampaignRecipients
            .Where(r => r.CampaignId == campaignId)
            .OrderByDescending(r => r.SentAt)
            .Skip(skip)
            .Take(Math.Min(100, Math.Max(1, take)))
            .ToListAsync(cancellationToken);
    }

    [GraphQLName("getCrmEmailTemplates")]
    public async Task<List<CrmEmailTemplate>> GetCrmEmailTemplates(
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        return await db.CrmEmailTemplates
            .Where(t => t.IsActive)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    [GraphQLName("getCrmSuppressionList")]
    public async Task<List<CrmEmailSuppressionList>> GetCrmSuppressionList(
        string? search,
        string? reason,
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        var query = db.CrmEmailSuppressionLists.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(x => x.EmailAddress.ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(reason))
        {
            query = query.Where(x => x.Reason == reason);
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
    }

    [GraphQLName("checkEmailSpamScore")]
    public SpamScoreResult CheckEmailSpamScore(
        string subject,
        string? bodyHtml,
        string? bodyText,
        [Service] ISpamScoringService spamScorer)
    {
        return spamScorer.EvaluateEmail(subject, bodyHtml, bodyText);
    }

    [GraphQLName("estimateCampaignAudience")]
    public async Task<AudienceEstimateResult> EstimateCampaignAudience(
        AudienceFilterInput? filter,
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        var query = db.CrmContacts.Where(c => c.IsActive);

        if (filter != null)
        {
            if (!string.IsNullOrWhiteSpace(filter.ContactCategory))
            {
                query = query.Where(c => c.ContactCategory == filter.ContactCategory);
            }
            if (!string.IsNullOrWhiteSpace(filter.State))
            {
                query = query.Where(c => c.State == filter.State);
            }
            if (!string.IsNullOrWhiteSpace(filter.City))
            {
                query = query.Where(c => c.City == filter.City);
            }
            if (!string.IsNullOrWhiteSpace(filter.RespCenter))
            {
                query = query.Where(c => c.RespCenter == filter.RespCenter);
            }
            if (!string.IsNullOrWhiteSpace(filter.Tag))
            {
                query = query.Where(c => c.Tags != null && c.Tags.Contains(filter.Tag));
            }
            if (filter.MinQualityScore.HasValue)
            {
                query = query.Where(c => c.QualityScore >= filter.MinQualityScore.Value);
            }
        }

        var total = await query.CountAsync(cancellationToken);

        // Fetch contacts with emails
        var contactsWithEmails = await query
            .Where(c => !string.IsNullOrEmpty(c.EmailIds))
            .Select(c => c.EmailIds!)
            .ToListAsync(cancellationToken);

        var validEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var emailField in contactsWithEmails)
        {
            var parts = emailField.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                var clean = p.Trim();
                if (clean.Contains('@') && clean.Contains('.'))
                {
                    validEmails.Add(clean);
                }
            }
        }

        var emailList = validEmails.ToList();
        var suppressedCount = await db.CrmEmailSuppressionLists
            .CountAsync(s => emailList.Contains(s.EmailAddress), cancellationToken);

        return new AudienceEstimateResult
        {
            TotalMatchingContacts = total,
            WithValidEmail = validEmails.Count,
            SuppressedCount = suppressedCount,
            EligibleRecipients = Math.Max(0, validEmails.Count - suppressedCount)
        };
    }
}

[ExtendObjectType(typeof(Mutation))]
public class CrmCampaignMutationExtension
{
    [GraphQLName("saveCrmEmailCampaign")]
    public async Task<CrmEmailCampaign> SaveCrmEmailCampaign(
        SaveCrmEmailCampaignInput input,
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        CrmEmailCampaign campaign;

        if (input.Id.HasValue && input.Id.Value != Guid.Empty)
        {
            campaign = await db.CrmEmailCampaigns.FirstOrDefaultAsync(c => c.Id == input.Id.Value, cancellationToken)
                       ?? throw new Exception("Campaign not found.");

            if (campaign.Status != "Draft" && campaign.Status != "Paused")
            {
                throw new Exception("Only Draft or Paused campaigns can be edited.");
            }
        }
        else
        {
            campaign = new CrmEmailCampaign
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            };
            db.CrmEmailCampaigns.Add(campaign);
        }

        campaign.Name = input.Name;
        campaign.Subject = input.Subject;
        campaign.PreviewText = input.PreviewText;
        campaign.FromName = input.FromName;
        campaign.FromEmail = input.FromEmail;
        campaign.ReplyToEmail = input.ReplyToEmail;
        campaign.CampaignType = input.CampaignType;
        campaign.ContentType = input.ContentType;
        campaign.BodyHtml = input.BodyHtml;
        campaign.BodyText = input.BodyText;
        campaign.UpdatedAt = DateTime.UtcNow;

        if (input.AudienceFilter != null)
        {
            campaign.TargetSegmentFilterJson = JsonSerializer.Serialize(input.AudienceFilter);
        }

        await db.SaveChangesAsync(cancellationToken);
        return campaign;
    }

    [GraphQLName("scheduleCrmEmailCampaign")]
    public async Task<CrmEmailCampaign> ScheduleCrmEmailCampaign(
        Guid campaignId,
        DateTime? scheduledAt,
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        var campaign = await db.CrmEmailCampaigns.FirstOrDefaultAsync(c => c.Id == campaignId, cancellationToken)
                       ?? throw new Exception("Campaign not found.");

        if (campaign.Status != "Draft" && campaign.Status != "Paused")
        {
            throw new Exception("Only Draft campaigns can be scheduled.");
        }

        // Parse Audience Filter
        AudienceFilterInput? filter = null;
        if (!string.IsNullOrEmpty(campaign.TargetSegmentFilterJson))
        {
            try
            {
                filter = JsonSerializer.Deserialize<AudienceFilterInput>(campaign.TargetSegmentFilterJson);
            }
            catch { }
        }

        // Fetch contacts matching filter
        var contactQuery = db.CrmContacts.Where(c => c.IsActive && !string.IsNullOrEmpty(c.EmailIds));

        if (filter != null)
        {
            if (!string.IsNullOrWhiteSpace(filter.ContactCategory))
                contactQuery = contactQuery.Where(c => c.ContactCategory == filter.ContactCategory);
            if (!string.IsNullOrWhiteSpace(filter.State))
                contactQuery = contactQuery.Where(c => c.State == filter.State);
            if (!string.IsNullOrWhiteSpace(filter.City))
                contactQuery = contactQuery.Where(c => c.City == filter.City);
            if (!string.IsNullOrWhiteSpace(filter.RespCenter))
                contactQuery = contactQuery.Where(c => c.RespCenter == filter.RespCenter);
            if (!string.IsNullOrWhiteSpace(filter.Tag))
                contactQuery = contactQuery.Where(c => c.Tags != null && c.Tags.Contains(filter.Tag));
            if (filter.MinQualityScore.HasValue)
                contactQuery = contactQuery.Where(c => c.QualityScore >= filter.MinQualityScore.Value);
        }

        var contacts = await contactQuery.ToListAsync(cancellationToken);

        // Fetch Suppression List to exclude upfront
        var suppressedEmails = await db.CrmEmailSuppressionLists
            .Select(s => s.EmailAddress.ToLower())
            .ToListAsync(cancellationToken);
        var suppressedSet = new HashSet<string>(suppressedEmails, StringComparer.OrdinalIgnoreCase);

        // Clear any previous queued recipients if re-scheduling
        var existingRecipients = await db.CrmEmailCampaignRecipients
            .Where(r => r.CampaignId == campaignId && r.Status == "Queued")
            .ToListAsync(cancellationToken);
        db.CrmEmailCampaignRecipients.RemoveRange(existingRecipients);

        var recipientsToAdd = new List<CrmEmailCampaignRecipient>();
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var c in contacts)
        {
            var rawEmails = c.EmailIds?.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            foreach (var email in rawEmails)
            {
                var clean = email.Trim().ToLowerInvariant();
                if (!clean.Contains('@') || !clean.Contains('.')) continue;

                // Check suppression and deduplication
                if (suppressedSet.Contains(clean)) continue;
                if (seenEmails.Contains(clean)) continue;

                seenEmails.Add(clean);

                recipientsToAdd.Add(new CrmEmailCampaignRecipient
                {
                    Id = Guid.NewGuid(),
                    CampaignId = campaignId,
                    ContactId = c.Id,
                    EmailAddress = clean,
                    FullName = c.FullName,
                    CompanyName = c.CompanyName,
                    Status = "Queued",
                    TrackingToken = Guid.NewGuid().ToString("N"),
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        db.CrmEmailCampaignRecipients.AddRange(recipientsToAdd);

        campaign.TotalRecipients = recipientsToAdd.Count;
        campaign.ScheduledAt = scheduledAt ?? DateTime.UtcNow;
        campaign.Status = "Scheduled";
        campaign.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return campaign;
    }

    [GraphQLName("pauseCrmEmailCampaign")]
    public async Task<CrmEmailCampaign> PauseCrmEmailCampaign(
        Guid campaignId,
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        var campaign = await db.CrmEmailCampaigns.FirstOrDefaultAsync(c => c.Id == campaignId, cancellationToken)
                       ?? throw new Exception("Campaign not found.");

        if (campaign.Status == "Scheduled" || campaign.Status == "InProgress")
        {
            campaign.Status = "Paused";
            campaign.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return campaign;
    }

    [GraphQLName("resumeCrmEmailCampaign")]
    public async Task<CrmEmailCampaign> ResumeCrmEmailCampaign(
        Guid campaignId,
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        var campaign = await db.CrmEmailCampaigns.FirstOrDefaultAsync(c => c.Id == campaignId, cancellationToken)
                       ?? throw new Exception("Campaign not found.");

        if (campaign.Status == "Paused")
        {
            campaign.Status = "InProgress";
            campaign.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return campaign;
    }

    [GraphQLName("cancelCrmEmailCampaign")]
    public async Task<CrmEmailCampaign> CancelCrmEmailCampaign(
        Guid campaignId,
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        var campaign = await db.CrmEmailCampaigns.FirstOrDefaultAsync(c => c.Id == campaignId, cancellationToken)
                       ?? throw new Exception("Campaign not found.");

        campaign.Status = "Cancelled";
        campaign.UpdatedAt = DateTime.UtcNow;

        // Remove or mark unsent recipients
        var unsent = await db.CrmEmailCampaignRecipients
            .Where(r => r.CampaignId == campaignId && r.Status == "Queued")
            .ToListAsync(cancellationToken);

        foreach (var r in unsent)
        {
            r.Status = "Cancelled";
        }

        await db.SaveChangesAsync(cancellationToken);
        return campaign;
    }

    [GraphQLName("sendTestCampaignEmail")]
    public async Task<bool> SendTestCampaignEmail(
        Guid campaignId,
        string targetEmail,
        [Service] CrmDbContext db,
        [Service] IEmailCampaignSenderService sender,
        CancellationToken cancellationToken)
    {
        var campaign = await db.CrmEmailCampaigns.FirstOrDefaultAsync(c => c.Id == campaignId, cancellationToken)
                       ?? throw new Exception("Campaign not found.");

        return await sender.SendDirectTestEmailAsync(
            targetEmail,
            campaign.Subject,
            campaign.BodyHtml ?? campaign.BodyText ?? "Test Content",
            campaign.BodyText,
            campaign.FromName,
            campaign.FromEmail,
            cancellationToken);
    }

    [GraphQLName("saveCrmEmailTemplate")]
    public async Task<CrmEmailTemplate> SaveCrmEmailTemplate(
        SaveCrmEmailTemplateInput input,
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        CrmEmailTemplate template;

        if (input.Id.HasValue && input.Id.Value != Guid.Empty)
        {
            template = await db.CrmEmailTemplates.FirstOrDefaultAsync(t => t.Id == input.Id.Value, cancellationToken)
                       ?? throw new Exception("Template not found.");
        }
        else
        {
            template = new CrmEmailTemplate
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            };
            db.CrmEmailTemplates.Add(template);
        }

        template.Name = input.Name;
        template.Category = input.Category;
        template.Subject = input.Subject;
        template.PreviewText = input.PreviewText;
        template.BodyHtml = input.BodyHtml;
        template.BodyText = input.BodyText;
        template.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return template;
    }

    [GraphQLName("deleteCrmEmailTemplate")]
    public async Task<bool> DeleteCrmEmailTemplate(
        Guid id,
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        var template = await db.CrmEmailTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (template != null)
        {
            template.IsActive = false;
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        return false;
    }

    [GraphQLName("addSuppressionEmail")]
    public async Task<CrmEmailSuppressionList> AddSuppressionEmail(
        string email,
        string reason,
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        var clean = email.Trim().ToLowerInvariant();
        var existing = await db.CrmEmailSuppressionLists.FirstOrDefaultAsync(s => s.EmailAddress.ToLower() == clean, cancellationToken);
        if (existing != null) return existing;

        var suppression = new CrmEmailSuppressionList
        {
            Id = Guid.NewGuid(),
            EmailAddress = email.Trim(),
            Reason = reason,
            CreatedAt = DateTime.UtcNow
        };
        db.CrmEmailSuppressionLists.Add(suppression);
        await db.SaveChangesAsync(cancellationToken);
        return suppression;
    }

    [GraphQLName("removeSuppressionEmail")]
    public async Task<bool> RemoveSuppressionEmail(
        string email,
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        var clean = email.Trim().ToLowerInvariant();
        var existing = await db.CrmEmailSuppressionLists.FirstOrDefaultAsync(s => s.EmailAddress.ToLower() == clean, cancellationToken);
        if (existing != null)
        {
            db.CrmEmailSuppressionLists.Remove(existing);
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        return false;
    }
}
