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
using Tyresoles.Web.Services.Whatsapp;

namespace Tyresoles.Web.GraphQL;

public class AudienceWhatsappFilterInput
{
    public string? ContactType { get; set; }
    public string? ContactCategory { get; set; }
    public string? State { get; set; }
    public string? City { get; set; }
    public string? RespCenter { get; set; }
    public string? Tag { get; set; }
    public decimal? MinQualityScore { get; set; }
    public string? Search { get; set; }
    public List<Guid>? SelectedContactIds { get; set; }
    public bool? OnlyUniqueNumbers { get; set; }
}

public class CrmContactWhatsappPickerItem
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? MobileNo { get; set; }
    public string? MobileNo2 { get; set; }
    public string? CleanWhatsappPhone { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ContactType { get; set; }
    public string? ContactCategory { get; set; }
    public string? RespCenter { get; set; }
    public decimal? QualityScore { get; set; }
    public bool IsUniqueNumber { get; set; } = true;
    public int PreviousCampaignCount { get; set; } = 0;
}

public class CrmContactsFilterOptionsResult
{
    public List<string> ContactTypes { get; set; } = new();
    public List<string> ContactCategories { get; set; } = new();
    public List<string> RespCenters { get; set; } = new();
    public List<string> States { get; set; } = new();
    public List<string> Cities { get; set; } = new();
}

public class CrmContactsWhatsappPickerResult
{
    public List<CrmContactWhatsappPickerItem> Items { get; set; } = new();
    public int TotalCount { get; set; }
}

public class CrmWhatsappInboundMessageDto
{
    public Guid Id { get; set; }
    public string FromPhoneNumber { get; set; } = string.Empty;
    public string? ProfileName { get; set; }
    public string MetaMessageId { get; set; } = string.Empty;
    public string? ContextWamid { get; set; }
    public Guid? CampaignId { get; set; }
    public string? CampaignName { get; set; }
    public Guid? ContactId { get; set; }
    public string? ContactFullName { get; set; }
    public string? CompanyName { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string MessageType { get; set; } = "text";
    public string? MessageBody { get; set; }
    public string? ButtonPayload { get; set; }
    public bool IsProcessed { get; set; }
    public string FollowupStatus { get; set; } = "Pending";
    public DateTime ReceivedAt { get; set; }
}

public class CrmWhatsappInboundMessagesResult
{
    public List<CrmWhatsappInboundMessageDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PendingCount { get; set; }
    public int ContactedCount { get; set; }
    public int ConvertedCount { get; set; }
}

public class CrmWhatsappWebhookLogDto
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = "unknown";
    public string? FromPhoneNumber { get; set; }
    public string? MetaMessageId { get; set; }
    public string ProcessingStatus { get; set; } = "Received";
    public string RawPayload { get; set; } = string.Empty;
    public string? SignatureHeader { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? CampaignId { get; set; }
    public string? CampaignName { get; set; }
    public Guid? RecipientId { get; set; }
    public DateTime ReceivedAt { get; set; }
}

public class CrmWhatsappWebhookLogsResult
{
    public List<CrmWhatsappWebhookLogDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int ProcessedCount { get; set; }
    public int SimulatedCount { get; set; }
    public int ErrorCount { get; set; }
}

public class SimulateWhatsappWebhookInput
{
    public string EventType { get; set; } = "messages"; // "messages", "delivered", "read", "failed", "button", "stop"
    public string? FromPhoneNumber { get; set; } = "919876543210";
    public string? CustomerName { get; set; } = "Test Fleet Customer";
    public string? MessageText { get; set; } = "Hello, I am interested in truck tyre retreading quote.";
    public string? ButtonPayload { get; set; }
    public string? MetaMessageId { get; set; }
    public Guid? CampaignId { get; set; }
    public Guid? RecipientId { get; set; }
}

public class SimulateWhatsappWebhookResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? LogId { get; set; }
    public Guid? InboundMessageId { get; set; }
    public string? MetaMessageId { get; set; }
}

public class WhatsappGroupTestResult
{
    public string PhoneNumber { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Wamid { get; set; }
    public int? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}

public class AudienceWhatsappEstimateResult
{
    public int TotalMatchingContacts { get; set; }
    public int WithValidPhone { get; set; }
    public int SuppressedCount { get; set; }
    public int PreviouslyCampaignedCount { get; set; }
    public int EligibleRecipients { get; set; }
}

public class VariableMappingInput
{
    public string Placeholder { get; set; } = string.Empty; // e.g. "1" or "{{1}}"
    public string FieldName { get; set; } = string.Empty; // e.g. "FullName", "CompanyName", "City"
}

public class SaveCrmWhatsappCampaignInput
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? TemplateId { get; set; }
    public string? SenderPhoneNumberId { get; set; }
    public string? DisplayPhoneNumber { get; set; }
    public string? HeaderMediaUrl { get; set; }
    public AudienceWhatsappFilterInput? TargetSegmentFilter { get; set; }
    public List<VariableMappingInput>? VariableMappings { get; set; }
}

public class SaveWhatsappSettingsInput
{
    public string? WabaId { get; set; }
    public string? PhoneNumberId { get; set; }
    public string? DisplayPhoneNumber { get; set; }
    public string? AccessToken { get; set; }
    public string? AppSecret { get; set; }
    public string? WebhookVerifyToken { get; set; }
    public bool? SimulationMode { get; set; }
    public decimal? CostPerMessage { get; set; }
}

[ExtendObjectType(typeof(Query))]
public class CrmWhatsappCampaignQueryExtension
{
    [GraphQLName("getCrmWhatsappCampaigns")]
    public async Task<List<CrmWhatsappCampaign>> GetCrmWhatsappCampaigns(
        string? status,
        int? skip,
        int? take,
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        var query = db.CrmWhatsappCampaigns
            .Include(c => c.Template)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && status != "ALL")
        {
            query = query.Where(c => c.Status == status);
        }

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip(skip ?? 0)
            .Take(Math.Min(100, Math.Max(1, take ?? 50)))
            .ToListAsync(cancellationToken);
    }

    [GraphQLName("getCrmWhatsappCampaignDetails")]
    public async Task<CrmWhatsappCampaign?> GetCrmWhatsappCampaignDetails(
        Guid id,
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        return await db.CrmWhatsappCampaigns
            .Include(c => c.Template)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    [GraphQLName("getCrmWhatsappCampaignRecipients")]
    public async Task<List<CrmWhatsappCampaignRecipient>> GetCrmWhatsappCampaignRecipients(
        Guid campaignId,
        string? status,
        int? skip,
        int? take,
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        var query = db.CrmWhatsappCampaignRecipients
            .Include(r => r.Contact)
            .Where(r => r.CampaignId == campaignId);

        if (!string.IsNullOrWhiteSpace(status) && status != "ALL")
        {
            query = query.Where(r => r.Status == status);
        }

        return await query
            .OrderByDescending(r => r.SentAt ?? r.CreatedAt)
            .Skip(skip ?? 0)
            .Take(Math.Min(100, Math.Max(1, take ?? 50)))
            .ToListAsync(cancellationToken);
    }

    [GraphQLName("getCrmWhatsappTemplates")]
    public async Task<List<CrmWhatsappTemplate>> GetCrmWhatsappTemplates(
        string? category,
        string? status,
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        var query = db.CrmWhatsappTemplates.AsQueryable();

        if (!string.IsNullOrWhiteSpace(category) && category != "ALL")
        {
            query = query.Where(t => t.Category == category);
        }
        if (!string.IsNullOrWhiteSpace(status) && status != "ALL")
        {
            query = query.Where(t => t.Status == status);
        }

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    [GraphQLName("getCrmWhatsappSuppressionList")]
    public async Task<List<CrmWhatsappSuppressionList>> GetCrmWhatsappSuppressionList(
        string? search,
        int? skip,
        int? take,
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        var query = db.CrmWhatsappSuppressionLists.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(x => x.PhoneNumber.Contains(s) || (x.Notes != null && x.Notes.Contains(s)));
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip ?? 0)
            .Take(Math.Min(200, Math.Max(1, take ?? 50)))
            .ToListAsync(cancellationToken);
    }

    [GraphQLName("estimateWhatsappCampaignAudience")]
    public async Task<AudienceWhatsappEstimateResult> EstimateWhatsappCampaignAudience(
        AudienceWhatsappFilterInput? filter,
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        var query = db.CrmContacts.Where(c => c.IsActive);

        if (filter != null)
        {
            if (filter.SelectedContactIds != null)
            {
                if (filter.SelectedContactIds.Count > 0)
                    query = query.Where(c => filter.SelectedContactIds.Contains(c.Id));
                else
                    query = query.Where(c => false);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(filter.Search))
                {
                    var s = filter.Search.Trim();
                    query = query.Where(c => (c.FullName != null && c.FullName.Contains(s)) ||
                                             (c.CompanyName != null && c.CompanyName.Contains(s)) ||
                                             (c.MobileNo != null && c.MobileNo.Contains(s)) ||
                                             (c.MobileNo2 != null && c.MobileNo2.Contains(s)) ||
                                             (c.City != null && c.City.Contains(s)));
                }
                if (!string.IsNullOrWhiteSpace(filter.ContactType))
                    query = query.Where(c => c.ContactType == filter.ContactType);
                if (!string.IsNullOrWhiteSpace(filter.ContactCategory))
                    query = query.Where(c => c.ContactCategory == filter.ContactCategory);
                if (!string.IsNullOrWhiteSpace(filter.State))
                    query = query.Where(c => c.State == filter.State);
                if (!string.IsNullOrWhiteSpace(filter.City))
                    query = query.Where(c => c.City == filter.City);
                if (!string.IsNullOrWhiteSpace(filter.RespCenter))
                    query = query.Where(c => c.RespCenter == filter.RespCenter);
                if (!string.IsNullOrWhiteSpace(filter.Tag))
                    query = query.Where(c => c.Tags != null && c.Tags.Contains(filter.Tag));
                if (filter.MinQualityScore.HasValue)
                    query = query.Where(c => c.QualityScore >= filter.MinQualityScore.Value);
            }
        }

        var total = await query.CountAsync(cancellationToken);

        // Fetch numbers
        var contactsWithPhones = await query
            .Where(c => !string.IsNullOrEmpty(c.MobileNo) || !string.IsNullOrEmpty(c.MobileNo2))
            .Select(c => new { c.MobileNo, c.MobileNo2 })
            .ToListAsync(cancellationToken);

        var validPhones = new HashSet<string>();
        foreach (var c in contactsWithPhones)
        {
            var p1 = WhatsappCloudApiService.NormalizePhoneNumber(c.MobileNo ?? "");
            if (p1.Length >= 10) validPhones.Add(p1);

            var p2 = WhatsappCloudApiService.NormalizePhoneNumber(c.MobileNo2 ?? "");
            if (p2.Length >= 10) validPhones.Add(p2);
        }

        var phoneList = validPhones.ToList();
        var suppressedPhones = await db.CrmWhatsappSuppressionLists
            .Where(s => phoneList.Contains(s.PhoneNumber))
            .Select(s => s.PhoneNumber)
            .ToListAsync(cancellationToken);
        var suppressedSet = new HashSet<string>(suppressedPhones, StringComparer.OrdinalIgnoreCase);

        // Check against previous campaigns
        var previousPhones = await db.CrmWhatsappCampaignRecipients
            .Where(r => phoneList.Contains(r.PhoneNumber))
            .Select(r => r.PhoneNumber)
            .Distinct()
            .ToListAsync(cancellationToken);
        var previousPhonesSet = new HashSet<string>(previousPhones, StringComparer.OrdinalIgnoreCase);

        var previouslyCampaignedCount = previousPhonesSet.Count;

        int eligibleRecipients;
        if (filter?.OnlyUniqueNumbers == true)
        {
            eligibleRecipients = validPhones.Count(p => !suppressedSet.Contains(p) && !previousPhonesSet.Contains(p));
        }
        else
        {
            eligibleRecipients = validPhones.Count(p => !suppressedSet.Contains(p));
        }

        return new AudienceWhatsappEstimateResult
        {
            TotalMatchingContacts = total,
            WithValidPhone = validPhones.Count,
            SuppressedCount = suppressedSet.Count,
            PreviouslyCampaignedCount = previouslyCampaignedCount,
            EligibleRecipients = eligibleRecipients
        };
    }

    [GraphQLName("getCrmContactsFilterOptions")]
    public async Task<CrmContactsFilterOptionsResult> GetCrmContactsFilterOptions(
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        var contactTypesFromContacts = await db.CrmContacts
            .Where(c => c.IsActive && !string.IsNullOrEmpty(c.ContactType))
            .Select(c => c.ContactType!)
            .Distinct()
            .ToListAsync(cancellationToken);

        var contactTypesFromMaster = await db.CrmContactTypes
            .Where(t => !string.IsNullOrEmpty(t.Name))
            .Select(t => t.Name)
            .ToListAsync(cancellationToken);

        var contactTypes = contactTypesFromContacts
            .Union(contactTypesFromMaster)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        var contactCategoriesFromContacts = await db.CrmContacts
            .Where(c => c.IsActive && !string.IsNullOrEmpty(c.ContactCategory))
            .Select(c => c.ContactCategory!)
            .Distinct()
            .ToListAsync(cancellationToken);

        var contactCategoriesFromMaster = await db.CrmContactCategories
            .Where(c => !string.IsNullOrEmpty(c.Name))
            .Select(c => c.Name)
            .ToListAsync(cancellationToken);

        var contactCategories = contactCategoriesFromContacts
            .Union(contactCategoriesFromMaster)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        var respCenters = await db.CrmContacts
            .Where(c => c.IsActive && !string.IsNullOrEmpty(c.RespCenter))
            .Select(c => c.RespCenter!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        var states = await db.CrmContacts
            .Where(c => c.IsActive && !string.IsNullOrEmpty(c.State))
            .Select(c => c.State!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        var cities = await db.CrmContacts
            .Where(c => c.IsActive && !string.IsNullOrEmpty(c.City))
            .Select(c => c.City!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        return new CrmContactsFilterOptionsResult
        {
            ContactTypes = contactTypes,
            ContactCategories = contactCategories,
            RespCenters = respCenters,
            States = states,
            Cities = cities
        };
    }

    [GraphQLName("getCrmContactsForWhatsappPicker")]
    public async Task<CrmContactsWhatsappPickerResult> GetCrmContactsForWhatsappPicker(
        string? search,
        string? contactType,
        string? contactCategory,
        string? state,
        string? city,
        string? respCenter,
        bool? onlyUniqueNumbers,
        int? skip,
        int? take,
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        var query = db.CrmContacts.Where(c => c.IsActive && (!string.IsNullOrEmpty(c.MobileNo) || !string.IsNullOrEmpty(c.MobileNo2)));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(c => (c.FullName != null && c.FullName.Contains(s)) ||
                                     (c.CompanyName != null && c.CompanyName.Contains(s)) ||
                                     (c.MobileNo != null && c.MobileNo.Contains(s)) ||
                                     (c.MobileNo2 != null && c.MobileNo2.Contains(s)) ||
                                     (c.City != null && c.City.Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(contactType) && contactType != "ALL")
            query = query.Where(c => c.ContactType == contactType);

        if (!string.IsNullOrWhiteSpace(contactCategory) && contactCategory != "ALL")
            query = query.Where(c => c.ContactCategory == contactCategory);

        if (!string.IsNullOrWhiteSpace(state) && state != "ALL")
            query = query.Where(c => c.State == state);

        if (!string.IsNullOrWhiteSpace(city) && city != "ALL")
            query = query.Where(c => c.City == city);

        if (!string.IsNullOrWhiteSpace(respCenter) && respCenter != "ALL")
            query = query.Where(c => c.RespCenter == respCenter);

        if (onlyUniqueNumbers == true)
        {
            var usedContactIds = db.CrmWhatsappCampaignRecipients
                .Where(r => r.ContactId != null)
                .Select(r => r.ContactId!.Value);

            query = query.Where(c => !usedContactIds.Contains(c.Id));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var pageSize = Math.Min(2000, Math.Max(1, take ?? 25));
        var pageSkip = Math.Max(0, skip ?? 0);

        var rawList = await query
            .OrderBy(c => c.FullName)
            .Skip(pageSkip)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id,
                c.FullName,
                c.CompanyName,
                c.MobileNo,
                c.MobileNo2,
                c.City,
                c.State,
                c.ContactType,
                c.ContactCategory,
                c.RespCenter,
                c.QualityScore
            })
            .ToListAsync(cancellationToken);

        var contactIds = rawList.Select(c => c.Id).ToList();
        var phones = rawList.SelectMany(c => new[] {
            WhatsappCloudApiService.NormalizePhoneNumber(c.MobileNo ?? ""),
            WhatsappCloudApiService.NormalizePhoneNumber(c.MobileNo2 ?? "")
        }).Where(p => p.Length >= 10).Distinct().ToList();

        var campaignRecipients = await db.CrmWhatsappCampaignRecipients
            .Where(r => (r.ContactId.HasValue && contactIds.Contains(r.ContactId.Value)) || phones.Contains(r.PhoneNumber))
            .Select(r => new { r.ContactId, r.PhoneNumber, r.CampaignId })
            .ToListAsync(cancellationToken);

        var countByContactId = campaignRecipients
            .Where(r => r.ContactId.HasValue)
            .GroupBy(r => r.ContactId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.CampaignId).Distinct().Count());

        var countByPhone = campaignRecipients
            .GroupBy(r => r.PhoneNumber)
            .ToDictionary(g => g.Key, g => g.Select(x => x.CampaignId).Distinct().Count());

        var items = rawList.Select(c =>
        {
            var p1 = WhatsappCloudApiService.NormalizePhoneNumber(c.MobileNo ?? "");
            var p2 = WhatsappCloudApiService.NormalizePhoneNumber(c.MobileNo2 ?? "");
            var cleanPhone = p1.Length >= 10 ? p1 : (p2.Length >= 10 ? p2 : "");

            var campaignCount = 0;
            if (countByContactId.TryGetValue(c.Id, out var cCount))
                campaignCount = Math.Max(campaignCount, cCount);
            if (!string.IsNullOrEmpty(cleanPhone) && countByPhone.TryGetValue(cleanPhone, out var pCount))
                campaignCount = Math.Max(campaignCount, pCount);

            return new CrmContactWhatsappPickerItem
            {
                Id = c.Id,
                FullName = c.FullName ?? "Customer",
                CompanyName = c.CompanyName,
                MobileNo = c.MobileNo,
                MobileNo2 = c.MobileNo2,
                CleanWhatsappPhone = cleanPhone,
                City = c.City,
                State = c.State,
                ContactType = c.ContactType,
                ContactCategory = c.ContactCategory,
                RespCenter = c.RespCenter,
                QualityScore = c.QualityScore,
                IsUniqueNumber = campaignCount == 0,
                PreviousCampaignCount = campaignCount
            };
        }).ToList();

        return new CrmContactsWhatsappPickerResult
        {
            Items = items,
            TotalCount = totalCount
        };
    }

    [GraphQLName("getWhatsappAccountStatus")]
    public async Task<WabaHealthStatus> GetWhatsappAccountStatus(
        [Service] IWhatsappCloudApiService whatsappService,
        CancellationToken cancellationToken)
    {
        return await whatsappService.GetWabaHealthStatusAsync(cancellationToken);
    }

    [GraphQLName("getCrmWhatsappInboundMessages")]
    public async Task<CrmWhatsappInboundMessagesResult> GetCrmWhatsappInboundMessages(
        Guid? campaignId,
        string? followupStatus,
        string? search,
        int? skip,
        int? take,
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        var baseQuery = db.CrmWhatsappInboundMessages.AsQueryable();

        var pendingCount = await baseQuery.CountAsync(m => m.FollowupStatus == "Pending", cancellationToken);
        var contactedCount = await baseQuery.CountAsync(m => m.FollowupStatus == "Contacted", cancellationToken);
        var convertedCount = await baseQuery.CountAsync(m => m.FollowupStatus == "Converted", cancellationToken);

        var query = baseQuery;

        if (campaignId.HasValue)
        {
            query = query.Where(m => m.CampaignId == campaignId.Value);
        }

        if (!string.IsNullOrWhiteSpace(followupStatus) && followupStatus != "ALL")
        {
            query = query.Where(m => m.FollowupStatus == followupStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(m => m.FromPhoneNumber.Contains(s) ||
                                     (m.ProfileName != null && m.ProfileName.Contains(s)) ||
                                     (m.MessageBody != null && m.MessageBody.Contains(s)) ||
                                     (m.ButtonPayload != null && m.ButtonPayload.Contains(s)));
        }

        var total = await query.CountAsync(cancellationToken);

        var rawList = await query
            .OrderByDescending(m => m.ReceivedAt)
            .Skip(skip ?? 0)
            .Take(Math.Min(100, Math.Max(1, take ?? 50)))
            .ToListAsync(cancellationToken);

        var contactIds = rawList.Where(m => m.ContactId.HasValue).Select(m => m.ContactId!.Value).Distinct().ToList();
        var campaignIds = rawList.Where(m => m.CampaignId.HasValue).Select(m => m.CampaignId!.Value).Distinct().ToList();

        var contactsMap = await db.CrmContacts
            .Where(c => contactIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var campaignsMap = await db.CrmWhatsappCampaigns
            .Where(c => campaignIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var missingPhones = rawList.Where(m => !m.ContactId.HasValue).Select(m => m.FromPhoneNumber).Distinct().ToList();
        var fallbackContacts = new List<CrmContact>();
        if (missingPhones.Count > 0)
        {
            fallbackContacts = await db.CrmContacts
                .Where(c => c.MobileNo != null && missingPhones.Contains(c.MobileNo) ||
                            c.MobileNo2 != null && missingPhones.Contains(c.MobileNo2))
                .ToListAsync(cancellationToken);
        }

        var items = rawList.Select(m =>
        {
            CrmContact? contact = null;
            if (m.ContactId.HasValue && contactsMap.TryGetValue(m.ContactId.Value, out var c))
            {
                contact = c;
            }
            else
            {
                contact = fallbackContacts.FirstOrDefault(fc =>
                    WhatsappCloudApiService.NormalizePhoneNumber(fc.MobileNo ?? "") == m.FromPhoneNumber ||
                    WhatsappCloudApiService.NormalizePhoneNumber(fc.MobileNo2 ?? "") == m.FromPhoneNumber);
            }

            string? campaignName = null;
            if (m.CampaignId.HasValue && campaignsMap.TryGetValue(m.CampaignId.Value, out var cName))
            {
                campaignName = cName;
            }

            return new CrmWhatsappInboundMessageDto
            {
                Id = m.Id,
                FromPhoneNumber = m.FromPhoneNumber,
                ProfileName = m.ProfileName,
                MetaMessageId = m.MetaMessageId,
                ContextWamid = m.ContextWamid,
                CampaignId = m.CampaignId,
                CampaignName = campaignName,
                ContactId = contact?.Id ?? m.ContactId,
                ContactFullName = contact?.FullName,
                CompanyName = contact?.CompanyName,
                City = contact?.City,
                State = contact?.State,
                MessageType = m.MessageType,
                MessageBody = m.MessageBody,
                ButtonPayload = m.ButtonPayload,
                IsProcessed = m.IsProcessed,
                FollowupStatus = m.FollowupStatus ?? "Pending",
                ReceivedAt = m.ReceivedAt
            };
        }).ToList();

        return new CrmWhatsappInboundMessagesResult
        {
            Items = items,
            TotalCount = total,
            PendingCount = pendingCount,
            ContactedCount = contactedCount,
            ConvertedCount = convertedCount
        };
    }

    [GraphQLName("getCrmWhatsappWebhookLogs")]
    public async Task<CrmWhatsappWebhookLogsResult> GetCrmWhatsappWebhookLogs(
        string? eventType,
        string? processingStatus,
        int? skip,
        int? take,
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        var baseQuery = db.CrmWhatsappWebhookLogs.AsQueryable();

        var total = await baseQuery.CountAsync(cancellationToken);
        var processed = await baseQuery.CountAsync(x => x.ProcessingStatus == "Processed", cancellationToken);
        var simulated = await baseQuery.CountAsync(x => x.ProcessingStatus == "Simulated", cancellationToken);
        var errors = await baseQuery.CountAsync(x => x.ProcessingStatus == "Error" || x.ProcessingStatus == "SignatureMismatch", cancellationToken);

        var query = baseQuery;
        if (!string.IsNullOrWhiteSpace(eventType) && eventType != "ALL")
        {
            query = query.Where(x => x.EventType == eventType);
        }
        if (!string.IsNullOrWhiteSpace(processingStatus) && processingStatus != "ALL")
        {
            query = query.Where(x => x.ProcessingStatus == processingStatus);
        }

        var list = await query
            .OrderByDescending(x => x.ReceivedAt)
            .Skip(skip ?? 0)
            .Take(Math.Min(100, Math.Max(1, take ?? 50)))
            .ToListAsync(cancellationToken);

        // Enrich campaign names
        var campaignIds = list.Where(x => x.CampaignId.HasValue).Select(x => x.CampaignId!.Value).Distinct().ToList();
        var campaigns = await db.CrmWhatsappCampaigns
            .Where(c => campaignIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var items = list.Select(l => new CrmWhatsappWebhookLogDto
        {
            Id = l.Id,
            EventType = l.EventType,
            FromPhoneNumber = l.FromPhoneNumber,
            MetaMessageId = l.MetaMessageId,
            ProcessingStatus = l.ProcessingStatus,
            RawPayload = l.RawPayload,
            SignatureHeader = l.SignatureHeader,
            ErrorMessage = l.ErrorMessage,
            CampaignId = l.CampaignId,
            CampaignName = l.CampaignId.HasValue && campaigns.TryGetValue(l.CampaignId.Value, out var name) ? name : null,
            RecipientId = l.RecipientId,
            ReceivedAt = l.ReceivedAt
        }).ToList();

        return new CrmWhatsappWebhookLogsResult
        {
            Items = items,
            TotalCount = total,
            ProcessedCount = processed,
            SimulatedCount = simulated,
            ErrorCount = errors
        };
    }
}

[ExtendObjectType(typeof(Mutation))]
public class CrmWhatsappCampaignMutationExtension
{
    [GraphQLName("saveCrmWhatsappCampaign")]
    public async Task<CrmWhatsappCampaign> SaveCrmWhatsappCampaign(
        SaveCrmWhatsappCampaignInput input,
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        CrmWhatsappCampaign campaign;

        if (input.Id.HasValue && input.Id.Value != Guid.Empty)
        {
            campaign = await db.CrmWhatsappCampaigns.FirstOrDefaultAsync(c => c.Id == input.Id.Value, cancellationToken)
                       ?? throw new Exception("Campaign not found.");

            if (campaign.Status != "Draft" && campaign.Status != "Paused")
            {
                throw new Exception("Only Draft or Paused campaigns can be edited.");
            }
        }
        else
        {
            campaign = new CrmWhatsappCampaign
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            };
            db.CrmWhatsappCampaigns.Add(campaign);
        }

        campaign.Name = input.Name;
        campaign.TemplateId = input.TemplateId;
        campaign.SenderPhoneNumberId = input.SenderPhoneNumberId;
        campaign.DisplayPhoneNumber = input.DisplayPhoneNumber;
        campaign.HeaderMediaUrl = input.HeaderMediaUrl;

        if (input.TemplateId.HasValue)
        {
            var tpl = await db.CrmWhatsappTemplates.FindAsync(new object[] { input.TemplateId.Value }, cancellationToken);
            if (tpl != null)
            {
                campaign.TemplateName = tpl.Name;
                campaign.LanguageCode = tpl.LanguageCode ?? "en";
            }
        }

        if (input.TargetSegmentFilter != null)
        {
            campaign.TargetSegmentFilterJson = JsonSerializer.Serialize(input.TargetSegmentFilter);
        }

        if (input.VariableMappings != null && input.VariableMappings.Count > 0)
        {
            var mapDict = input.VariableMappings.ToDictionary(m => m.Placeholder, m => m.FieldName);
            campaign.VariableMappingsJson = JsonSerializer.Serialize(mapDict);
        }

        campaign.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return campaign;
    }

    [GraphQLName("scheduleCrmWhatsappCampaign")]
    public async Task<CrmWhatsappCampaign> ScheduleCrmWhatsappCampaign(
        Guid campaignId,
        DateTime? scheduledAt,
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        var campaign = await db.CrmWhatsappCampaigns
            .Include(c => c.Template)
            .FirstOrDefaultAsync(c => c.Id == campaignId, cancellationToken)
            ?? throw new Exception("Campaign not found.");

        if (campaign.Status != "Draft" && campaign.Status != "Paused")
        {
            throw new Exception("Only Draft or Paused campaigns can be scheduled.");
        }

        AudienceWhatsappFilterInput? filter = null;
        if (!string.IsNullOrEmpty(campaign.TargetSegmentFilterJson))
        {
            try
            {
                filter = JsonSerializer.Deserialize<AudienceWhatsappFilterInput>(campaign.TargetSegmentFilterJson);
            }
            catch { }
        }

        var contactQuery = db.CrmContacts.Where(c => c.IsActive && (!string.IsNullOrEmpty(c.MobileNo) || !string.IsNullOrEmpty(c.MobileNo2)));

        if (filter != null)
        {
            if (filter.SelectedContactIds != null)
            {
                if (filter.SelectedContactIds.Count > 0)
                    contactQuery = contactQuery.Where(c => filter.SelectedContactIds.Contains(c.Id));
                else
                    contactQuery = contactQuery.Where(c => false);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(filter.Search))
                {
                    var s = filter.Search.Trim();
                    contactQuery = contactQuery.Where(c => (c.FullName != null && c.FullName.Contains(s)) ||
                                                           (c.CompanyName != null && c.CompanyName.Contains(s)) ||
                                                           (c.MobileNo != null && c.MobileNo.Contains(s)) ||
                                                           (c.MobileNo2 != null && c.MobileNo2.Contains(s)) ||
                                                           (c.City != null && c.City.Contains(s)));
                }
                if (!string.IsNullOrWhiteSpace(filter.ContactType))
                    contactQuery = contactQuery.Where(c => c.ContactType == filter.ContactType);
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
        }

        var contacts = await contactQuery.ToListAsync(cancellationToken);

        // Fetch suppression list
        var suppressed = await db.CrmWhatsappSuppressionLists
            .Select(s => s.PhoneNumber)
            .ToListAsync(cancellationToken);
        var suppressedSet = new HashSet<string>(suppressed, StringComparer.OrdinalIgnoreCase);

        // Fetch previous campaign recipients if unique numbers only is requested
        HashSet<string>? previousCampaignPhones = null;
        HashSet<Guid>? previousCampaignContactIds = null;
        if (filter?.OnlyUniqueNumbers == true)
        {
            var prevRecipients = await db.CrmWhatsappCampaignRecipients
                .Where(r => r.CampaignId != campaignId)
                .Select(r => new { r.ContactId, r.PhoneNumber })
                .ToListAsync(cancellationToken);

            previousCampaignPhones = new HashSet<string>(prevRecipients.Select(r => r.PhoneNumber), StringComparer.OrdinalIgnoreCase);
            previousCampaignContactIds = new HashSet<Guid>(prevRecipients.Where(r => r.ContactId.HasValue).Select(r => r.ContactId!.Value));
        }

        // Remove any existing queued recipients
        var existingQueued = await db.CrmWhatsappCampaignRecipients
            .Where(r => r.CampaignId == campaignId && r.Status == "Queued")
            .ToListAsync(cancellationToken);
        db.CrmWhatsappCampaignRecipients.RemoveRange(existingQueued);

        var recipientsToAdd = new List<CrmWhatsappCampaignRecipient>();
        var seenPhones = new HashSet<string>();

        foreach (var c in contacts)
        {
            var phone = WhatsappCloudApiService.NormalizePhoneNumber(c.MobileNo ?? c.MobileNo2 ?? "");
            if (phone.Length < 10) continue;
            if (suppressedSet.Contains(phone)) continue;
            if (seenPhones.Contains(phone)) continue;

            if (filter?.OnlyUniqueNumbers == true)
            {
                if (previousCampaignPhones != null && previousCampaignPhones.Contains(phone)) continue;
                if (previousCampaignContactIds != null && previousCampaignContactIds.Contains(c.Id)) continue;
            }

            seenPhones.Add(phone);

            recipientsToAdd.Add(new CrmWhatsappCampaignRecipient
            {
                Id = Guid.NewGuid(),
                CampaignId = campaignId,
                ContactId = c.Id,
                PhoneNumber = phone,
                FullName = c.FullName ?? "Customer",
                CompanyName = c.CompanyName,
                Status = "Queued",
                CreatedAt = DateTime.UtcNow
            });
        }

        db.CrmWhatsappCampaignRecipients.AddRange(recipientsToAdd);

        campaign.TotalRecipients = recipientsToAdd.Count;
        campaign.EstimatedCost = campaign.TotalRecipients * campaign.CostPerMessage;
        campaign.ScheduledAt = scheduledAt ?? DateTime.UtcNow;
        campaign.Status = "Scheduled";
        campaign.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return campaign;
    }

    [GraphQLName("pauseCrmWhatsappCampaign")]
    public async Task<CrmWhatsappCampaign> PauseCrmWhatsappCampaign(
        Guid campaignId,
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        var campaign = await db.CrmWhatsappCampaigns.FirstOrDefaultAsync(c => c.Id == campaignId, cancellationToken)
                       ?? throw new Exception("Campaign not found.");

        if (campaign.Status == "InProgress" || campaign.Status == "Scheduled")
        {
            campaign.Status = "Paused";
            campaign.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return campaign;
    }

    [GraphQLName("resumeCrmWhatsappCampaign")]
    public async Task<CrmWhatsappCampaign> ResumeCrmWhatsappCampaign(
        Guid campaignId,
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        var campaign = await db.CrmWhatsappCampaigns.FirstOrDefaultAsync(c => c.Id == campaignId, cancellationToken)
                       ?? throw new Exception("Campaign not found.");

        if (campaign.Status == "Paused")
        {
            campaign.Status = "InProgress";
            campaign.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return campaign;
    }

    [GraphQLName("cancelCrmWhatsappCampaign")]
    public async Task<CrmWhatsappCampaign> CancelCrmWhatsappCampaign(
        Guid campaignId,
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        var campaign = await db.CrmWhatsappCampaigns.FirstOrDefaultAsync(c => c.Id == campaignId, cancellationToken)
                       ?? throw new Exception("Campaign not found.");

        campaign.Status = "Cancelled";
        campaign.UpdatedAt = DateTime.UtcNow;

        var queued = await db.CrmWhatsappCampaignRecipients
            .Where(r => r.CampaignId == campaignId && r.Status == "Queued")
            .ToListAsync(cancellationToken);

        foreach (var r in queued)
        {
            r.Status = "Cancelled";
        }

        await db.SaveChangesAsync(cancellationToken);
        return campaign;
    }

    [GraphQLName("deleteCrmWhatsappCampaign")]
    public async Task<bool> DeleteCrmWhatsappCampaign(
        Guid campaignId,
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        var campaign = await db.CrmWhatsappCampaigns.FirstOrDefaultAsync(c => c.Id == campaignId, cancellationToken);
        if (campaign == null) return false;

        db.CrmWhatsappCampaigns.Remove(campaign);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    [GraphQLName("testSendWhatsappCampaign")]
    public async Task<WhatsappSendResult> TestSendWhatsappCampaign(
        Guid campaignId,
        string testPhoneNumber,
        [Service] CrmDbContext db,
        [Service] IWhatsappCloudApiService whatsappApi,
        CancellationToken cancellationToken)
    {
        var campaign = await db.CrmWhatsappCampaigns
            .Include(c => c.Template)
            .FirstOrDefaultAsync(c => c.Id == campaignId, cancellationToken)
            ?? throw new Exception("Campaign not found.");

        var cleanPhone = WhatsappCloudApiService.NormalizePhoneNumber(testPhoneNumber);
        if (cleanPhone.Length < 10)
        {
            return new WhatsappSendResult { Success = false, ErrorMessage = "Invalid phone number format." };
        }

        string templateName = campaign.Template?.Name ?? campaign.TemplateName ?? "general_notice";
        string langCode = campaign.Template?.LanguageCode ?? campaign.LanguageCode ?? "en";

        // Build sample parameters
        var components = new List<WhatsappTemplateComponent>();

        var templateHeaderType = campaign.Template?.HeaderType?.ToUpperInvariant() ?? "NONE";
        var hasMediaHeader = templateHeaderType == "IMAGE" || templateHeaderType == "DOCUMENT" || templateHeaderType == "VIDEO"
            || (campaign.Template == null && !string.IsNullOrEmpty(campaign.HeaderMediaUrl));

        var effectiveMediaUrl = !string.IsNullOrWhiteSpace(campaign.HeaderMediaUrl)
            ? campaign.HeaderMediaUrl
            : campaign.Template?.HeaderMediaUrl;

        if (string.IsNullOrWhiteSpace(effectiveMediaUrl) && hasMediaHeader && !string.IsNullOrWhiteSpace(campaign.Template?.ComponentsJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(campaign.Template.ComponentsJson);
                foreach (var elem in doc.RootElement.EnumerateArray())
                {
                    if (elem.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "HEADER" &&
                        elem.TryGetProperty("example", out var exProp) &&
                        exProp.TryGetProperty("header_handle", out var hhProp) &&
                        hhProp.GetArrayLength() > 0)
                    {
                        effectiveMediaUrl = hhProp[0].GetString();
                        break;
                    }
                }
            }
            catch { }
        }

        if (hasMediaHeader)
        {
            if (string.IsNullOrWhiteSpace(effectiveMediaUrl))
            {
                return new WhatsappSendResult
                {
                    Success = false,
                    ErrorCode = 132012,
                    ErrorMessage = $"Template requires an {templateHeaderType} header, but no Header Media URL was provided."
                };
            }

            var isDoc = templateHeaderType == "DOCUMENT" || effectiveMediaUrl.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
            components.Add(new WhatsappTemplateComponent
            {
                Type = "header",
                Parameters = new List<WhatsappParameter>
                {
                    new()
                    {
                        Type = isDoc ? "document" : "image",
                        Image = isDoc ? null : new WhatsappMediaParam { Link = effectiveMediaUrl },
                        Document = isDoc ? new WhatsappMediaParam { Link = effectiveMediaUrl, Filename = "Offer.pdf" } : null
                    }
                }
            });
        }

        // Body Parameters based on variable mappings or template placeholders
        Dictionary<string, string> variableMappings = new();
        if (!string.IsNullOrEmpty(campaign.VariableMappingsJson))
        {
            try
            {
                variableMappings = JsonSerializer.Deserialize<Dictionary<string, string>>(campaign.VariableMappingsJson) 
                                   ?? new Dictionary<string, string>();
            }
            catch { }
        }

        if (variableMappings.Count > 0)
        {
            var sortedKeys = variableMappings.Keys
                .OrderBy(k => int.TryParse(k.Trim('{', '}'), out var num) ? num : 99)
                .ToList();

            var bodyParams = new List<WhatsappParameter>();
            foreach (var key in sortedKeys)
            {
                var fieldName = variableMappings[key];
                var sampleVal = fieldName switch
                {
                    "FullName" => "Rajesh Kumar",
                    "CompanyName" => "ABC Logistics",
                    "City" => "Pune",
                    "RespCenter" => "Tyresoles Pune Depot",
                    "Products" => "Commercial Tyre Retreads",
                    _ => "Sample"
                };
                bodyParams.Add(new WhatsappParameter { Type = "text", Text = sampleVal });
            }

            components.Add(new WhatsappTemplateComponent
            {
                Type = "body",
                Parameters = bodyParams
            });
        }
        else if (!string.IsNullOrEmpty(campaign.Template?.BodyText))
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(campaign.Template.BodyText, @"\{\{(\d+)\}\}");
            var distinctPlaceholders = matches.Select(m => m.Value).Distinct().ToList();
            if (distinctPlaceholders.Count > 0)
            {
                var bodyParams = new List<WhatsappParameter>();
                for (int i = 0; i < distinctPlaceholders.Count; i++)
                {
                    var sampleVal = i switch
                    {
                        0 => "Rajesh Kumar",
                        1 => "ABC Logistics",
                        2 => "Pune",
                        3 => "Tyresoles Pune Depot",
                        _ => $"Sample {i + 1}"
                    };
                    bodyParams.Add(new WhatsappParameter { Type = "text", Text = sampleVal });
                }

                components.Add(new WhatsappTemplateComponent
                {
                    Type = "body",
                    Parameters = bodyParams
                });
            }
        }

        return await whatsappApi.SendTemplateMessageAsync(cleanPhone, templateName, langCode, components, cancellationToken);
    }

    [GraphQLName("testSendWhatsappCampaignGroup")]
    public async Task<List<WhatsappGroupTestResult>> TestSendWhatsappCampaignGroup(
        Guid campaignId,
        List<string> testPhoneNumbers,
        [Service] CrmDbContext db,
        [Service] IWhatsappCloudApiService whatsappApi,
        CancellationToken cancellationToken)
    {
        var results = new List<WhatsappGroupTestResult>();
        if (testPhoneNumbers == null || testPhoneNumbers.Count == 0) return results;

        foreach (var rawPhone in testPhoneNumbers)
        {
            var cleanPhone = WhatsappCloudApiService.NormalizePhoneNumber(rawPhone);
            if (string.IsNullOrEmpty(cleanPhone) || cleanPhone.Length < 10)
            {
                results.Add(new WhatsappGroupTestResult
                {
                    PhoneNumber = rawPhone,
                    Success = false,
                    ErrorMessage = "Invalid phone format (must be 10+ digits)."
                });
                continue;
            }

            try
            {
                var sendRes = await TestSendWhatsappCampaign(campaignId, cleanPhone, db, whatsappApi, cancellationToken);
                results.Add(new WhatsappGroupTestResult
                {
                    PhoneNumber = cleanPhone,
                    Success = sendRes.Success,
                    Wamid = sendRes.Wamid,
                    ErrorCode = sendRes.ErrorCode,
                    ErrorMessage = sendRes.ErrorMessage
                });
            }
            catch (Exception ex)
            {
                results.Add(new WhatsappGroupTestResult
                {
                    PhoneNumber = cleanPhone,
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        return results;
    }

    [GraphQLName("syncWhatsappTemplatesFromMeta")]
    public async Task<List<CrmWhatsappTemplate>> SyncWhatsappTemplatesFromMeta(
        [Service] IWhatsappCloudApiService whatsappApi,
        CancellationToken cancellationToken)
    {
        return await whatsappApi.SyncTemplatesFromMetaAsync(cancellationToken);
    }

    [GraphQLName("submitWhatsappTemplateToMeta")]
    public async Task<CrmWhatsappTemplate> SubmitWhatsappTemplateToMeta(
        CreateTemplateInput input,
        [Service] IWhatsappCloudApiService whatsappApi,
        CancellationToken cancellationToken)
    {
        return await whatsappApi.CreateTemplateOnMetaAsync(input, cancellationToken);
    }

    [GraphQLName("addWhatsappSuppression")]
    public async Task<CrmWhatsappSuppressionList> AddWhatsappSuppression(
        string phoneNumber,
        string reason,
        string? notes,
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        var clean = WhatsappCloudApiService.NormalizePhoneNumber(phoneNumber);
        var existing = await db.CrmWhatsappSuppressionLists.FirstOrDefaultAsync(s => s.PhoneNumber == clean, cancellationToken);
        if (existing != null) return existing;

        var entry = new CrmWhatsappSuppressionList
        {
            Id = Guid.NewGuid(),
            PhoneNumber = clean,
            Reason = reason,
            Source = "Manual",
            Notes = notes,
            CreatedAt = DateTime.UtcNow
        };
        db.CrmWhatsappSuppressionLists.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return entry;
    }

    [GraphQLName("removeWhatsappSuppression")]
    public async Task<bool> RemoveWhatsappSuppression(
        Guid id,
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        var entry = await db.CrmWhatsappSuppressionLists.FindAsync(new object[] { id }, cancellationToken);
        if (entry == null) return false;

        db.CrmWhatsappSuppressionLists.Remove(entry);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    [GraphQLName("saveWhatsappSettings")]
    public async Task<bool> SaveWhatsappSettings(
        SaveWhatsappSettingsInput input,
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        async Task UpsertSetting(string key, string? value, string desc)
        {
            if (value == null) return;
            var s = await db.CrmSettings.FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
            if (s == null)
            {
                db.CrmSettings.Add(new CrmSetting { Key = key, Value = value, Description = desc });
            }
            else
            {
                s.Value = value;
            }
        }

        await UpsertSetting("WHATSAPP_WABA_ID", input.WabaId, "WhatsApp Business Account ID");
        await UpsertSetting("WHATSAPP_PHONE_NUMBER_ID", input.PhoneNumberId, "WhatsApp Phone Number ID");
        await UpsertSetting("WHATSAPP_DISPLAY_PHONE_NUMBER", input.DisplayPhoneNumber, "WhatsApp Display Phone Number");
        await UpsertSetting("WHATSAPP_ACCESS_TOKEN", input.AccessToken, "WhatsApp Meta Access Token");
        await UpsertSetting("WHATSAPP_APP_SECRET", input.AppSecret, "WhatsApp Meta App Secret");
        await UpsertSetting("WHATSAPP_WEBHOOK_VERIFY_TOKEN", input.WebhookVerifyToken, "WhatsApp Webhook Verify Token");
        if (input.SimulationMode.HasValue)
        {
            await UpsertSetting("WHATSAPP_SIMULATION_MODE", input.SimulationMode.Value.ToString(), "WhatsApp Simulation Mode Flag");
        }
        if (input.CostPerMessage.HasValue)
        {
            await UpsertSetting("WHATSAPP_COST_PER_MESSAGE", input.CostPerMessage.Value.ToString("F4"), "Cost per delivered marketing message");
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    [GraphQLName("updateWhatsappInboundFollowupStatus")]
    public async Task<bool> UpdateWhatsappInboundFollowupStatus(
        Guid id,
        string status,
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        var msg = await db.CrmWhatsappInboundMessages.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (msg == null) return false;

        msg.FollowupStatus = status;
        if (status == "Contacted" || status == "Converted")
        {
            msg.IsProcessed = true;
        }
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    [GraphQLName("simulateWhatsappWebhook")]
    public async Task<SimulateWhatsappWebhookResult> SimulateWhatsappWebhook(
        SimulateWhatsappWebhookInput input,
        [Service] CrmDbContext db,
        CancellationToken cancellationToken)
    {
        var cleanPhone = WhatsappCloudApiService.NormalizePhoneNumber(input.FromPhoneNumber ?? "919876543210");
        var wamid = string.IsNullOrWhiteSpace(input.MetaMessageId)
            ? $"wamid.simulated_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}"[..Math.Min(60, 50)]
            : input.MetaMessageId;

        var eventType = (input.EventType ?? "messages").ToLowerInvariant();
        var unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        string simulatedJson;
        if (eventType == "messages" || eventType == "text" || eventType == "button" || eventType == "stop")
        {
            var isButton = eventType == "button" || !string.IsNullOrEmpty(input.ButtonPayload);
            var isStop = eventType == "stop" || (input.MessageText ?? "").Trim().Equals("STOP", StringComparison.OrdinalIgnoreCase);

            var messageObj = isButton ? (object)new
            {
                from = cleanPhone,
                id = wamid,
                timestamp = unixTimestamp.ToString(),
                type = "button",
                button = new { text = input.MessageText ?? "Interested", payload = input.ButtonPayload ?? "QUICK_REPLY_YES" }
            } : new
            {
                from = cleanPhone,
                id = wamid,
                timestamp = unixTimestamp.ToString(),
                type = "text",
                text = new { body = isStop ? "STOP" : (input.MessageText ?? "Hi, I am interested in tyre retreading.") }
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
                                    contacts = new[] { new { profile = new { name = input.CustomerName ?? "Simulated Tester" }, wa_id = cleanPhone } },
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
            CampaignId = input.CampaignId,
            RecipientId = input.RecipientId,
            ReceivedAt = DateTime.UtcNow
        };
        db.CrmWhatsappWebhookLogs.Add(log);

        Guid? inboundId = null;

        if (eventType == "messages" || eventType == "text" || eventType == "button" || eventType == "stop")
        {
            var isStop = eventType == "stop" || (input.MessageText ?? "").Trim().Equals("STOP", StringComparison.OrdinalIgnoreCase);

            if (isStop)
            {
                var alreadySuppressed = await db.CrmWhatsappSuppressionLists.AnyAsync(s => s.PhoneNumber == cleanPhone, cancellationToken);
                if (!alreadySuppressed)
                {
                    db.CrmWhatsappSuppressionLists.Add(new CrmWhatsappSuppressionList
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
                CrmWhatsappCampaignRecipient? matchingRecipient = null;
                if (input.RecipientId.HasValue)
                {
                    matchingRecipient = await db.CrmWhatsappCampaignRecipients
                        .Include(r => r.Campaign)
                        .FirstOrDefaultAsync(r => r.Id == input.RecipientId.Value, cancellationToken);
                }
                else if (input.CampaignId.HasValue)
                {
                    matchingRecipient = await db.CrmWhatsappCampaignRecipients
                        .Include(r => r.Campaign)
                        .FirstOrDefaultAsync(r => r.CampaignId == input.CampaignId.Value && r.PhoneNumber == cleanPhone, cancellationToken);
                }
                if (matchingRecipient == null)
                {
                    matchingRecipient = await db.CrmWhatsappCampaignRecipients
                        .Include(r => r.Campaign)
                        .Where(r => r.PhoneNumber == cleanPhone)
                        .OrderByDescending(r => r.CreatedAt)
                        .FirstOrDefaultAsync(cancellationToken);
                }

                inboundId = Guid.NewGuid();
                var inbound = new CrmWhatsappInboundMessage
                {
                    Id = inboundId.Value,
                    FromPhoneNumber = cleanPhone,
                    ProfileName = input.CustomerName ?? "Simulated Tester",
                    MetaMessageId = wamid,
                    ContextWamid = matchingRecipient?.MetaMessageId,
                    CampaignId = input.CampaignId ?? matchingRecipient?.CampaignId,
                    ContactId = matchingRecipient?.ContactId,
                    MessageType = string.IsNullOrEmpty(input.ButtonPayload) ? "text" : "button",
                    MessageBody = input.MessageText ?? "Hi, I am interested in tyre retreading.",
                    ButtonPayload = input.ButtonPayload,
                    FollowupStatus = "Pending",
                    ReceivedAt = DateTime.UtcNow
                };
                db.CrmWhatsappInboundMessages.Add(inbound);

                if (matchingRecipient != null)
                {
                    matchingRecipient.Status = "Replied";
                    matchingRecipient.RepliedAt = DateTime.UtcNow;
                    matchingRecipient.ReplyMessageText = input.MessageText;
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
            CrmWhatsappCampaignRecipient? matchingRecipient = null;
            if (input.RecipientId.HasValue)
            {
                matchingRecipient = await db.CrmWhatsappCampaignRecipients
                    .Include(r => r.Campaign)
                    .FirstOrDefaultAsync(r => r.Id == input.RecipientId.Value, cancellationToken);
            }
            else if (!string.IsNullOrEmpty(input.MetaMessageId))
            {
                matchingRecipient = await db.CrmWhatsappCampaignRecipients
                    .Include(r => r.Campaign)
                    .FirstOrDefaultAsync(r => r.MetaMessageId == input.MetaMessageId, cancellationToken);
            }
            else if (input.CampaignId.HasValue)
            {
                matchingRecipient = await db.CrmWhatsappCampaignRecipients
                    .Include(r => r.Campaign)
                    .FirstOrDefaultAsync(r => r.CampaignId == input.CampaignId.Value && r.PhoneNumber == cleanPhone, cancellationToken);
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

        await db.SaveChangesAsync(cancellationToken);

        return new SimulateWhatsappWebhookResult
        {
            Success = true,
            Message = $"Simulated '{eventType}' webhook event processed and logged successfully.",
            LogId = log.Id,
            InboundMessageId = inboundId,
            MetaMessageId = wamid
        };
    }
}
