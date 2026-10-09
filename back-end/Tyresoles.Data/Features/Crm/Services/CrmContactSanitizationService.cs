using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Tyresoles.Data.Features.Crm.Entities;
using Tyresoles.Data.Features.Crm.Models;

namespace Tyresoles.Data.Features.Crm.Services;

public partial class CrmContactSanitizationService : ICrmContactSanitizationService
{
    private readonly CrmDbContext _crmDb;
    private readonly ILogger<CrmContactSanitizationService> _logger;
    private static readonly HttpClient _scraperClient;

    static CrmContactSanitizationService()
    {
        var cookieContainer = new CookieContainer();
        cookieContainer.Add(new Uri("https://transportfamily.com"), new Cookie("humans_21909", "1"));

        var handler = new SocketsHttpHandler
        {
            CookieContainer = cookieContainer,
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(15)
        };

        _scraperClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(12)
        };
        _scraperClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _scraperClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
        _scraperClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
    }

    public CrmContactSanitizationService(CrmDbContext crmDb, ILogger<CrmContactSanitizationService> logger)
    {
        _crmDb = crmDb;
        _logger = logger;
    }

    public static readonly Dictionary<string, string> StateNameToCodeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Indian States & Union Territories
        ["Andaman and Nicobar Islands"] = "AN",
        ["Andaman & Nicobar Islands"] = "AN",
        ["Andaman and Nicobar Island"] = "AN",
        ["Andaman & Nicobar Island"] = "AN",
        ["Andhra Pradesh"] = "AP",
        ["Arunachal Pradesh"] = "AR",
        ["Assam"] = "AS",
        ["Bihar"] = "BH",
        ["Chandigarh"] = "CH",
        ["Chhattisgarh"] = "CT",
        ["Dadra and Nagar Haveli and Daman and Diu"] = "DN",
        ["Dadra & Nagar Haveli and Daman & Diu"] = "DN",
        ["Dadra and Nagar Haveli"] = "DN",
        ["Dadra & Nagar Haveli"] = "DN",
        ["Daman and Diu"] = "DD",
        ["Daman & Diu"] = "DD",
        ["Delhi"] = "DL",
        ["NCT of Delhi"] = "DL",
        ["National Capital Territory of Delhi"] = "DL",
        ["Goa"] = "GA",
        ["Gujarat"] = "GJ",
        ["Haryana"] = "HR",
        ["Himachal Pradesh"] = "HP",
        ["Jammu and Kashmir"] = "JK",
        ["Jammu & Kashmir"] = "JK",
        ["Jharkhand"] = "JH",
        ["Karnataka"] = "KA",
        ["Kerala"] = "KL",
        ["Ladakh"] = "LA",
        ["Lakshadweep"] = "LD",
        ["Lakshadweep Island"] = "LD",
        ["Madhya Pradesh"] = "MP",
        ["Maharashtra"] = "MH",
        ["Manipur"] = "MN",
        ["Meghalaya"] = "ME",
        ["Mizoram"] = "MI",
        ["Nagaland"] = "NL",
        ["Odisha"] = "OR",
        ["Orissa"] = "OR",
        ["Puducherry"] = "PY",
        ["Pondicherry"] = "PY",
        ["Punjab"] = "PB",
        ["Rajasthan"] = "RJ",
        ["Sikkim"] = "SK",
        ["Tamil Nadu"] = "TN",
        ["Telangana"] = "TS",
        ["Tripura"] = "TR",
        ["Uttar Pradesh"] = "UP",
        ["Uttarakhand"] = "UT",
        ["Uttaranchal"] = "UT",
        ["West Bengal"] = "WB",

        // Common abbreviations to standard NAV codes
        ["BR"] = "BH",
        ["CG"] = "CT",
        ["TG"] = "TS",
        ["UK"] = "UT",
        ["OD"] = "OR",
        ["AD"] = "AP",

        // International
        ["Malaysia"] = "MY",
        ["Mexico"] = "MX",
        ["UAE"] = "AE",
        ["United Arab Emirates"] = "AE"
    };

    public static string NormalizeState(string? state)
    {
        if (string.IsNullOrWhiteSpace(state)) return string.Empty;
        var trimmed = state.Trim();

        if (StateNameToCodeMap.TryGetValue(trimmed, out var code))
            return code;

        if (trimmed.Length == 2)
            return trimmed.ToUpperInvariant();

        return trimmed;
    }

    public static List<string> CleanTagsList(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags)) return new List<string>();

        return tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => !string.Equals(t, "Tyresoles", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(t, "Tyresoles Ltd", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(t, "Tyresoles India", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<CrmContactSanitizationStatsDto> GetStatsAsync(CancellationToken ct = default)
    {
        var stats = new CrmContactSanitizationStatsDto();

        var conn = _crmDb.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(ct).ConfigureAwait(false);

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT 
                    COUNT(*) AS TotalContacts,
                    SUM(CASE WHEN LEN(LTRIM(RTRIM(ISNULL(State, '')))) > 2 THEN 1 ELSE 0 END) AS UnalignedStateCount,
                    SUM(CASE WHEN LEN(LTRIM(RTRIM(ISNULL(State, '')))) = 2 THEN 1 ELSE 0 END) AS AlignedStateCount,
                    SUM(CASE WHEN Tags LIKE '%Tyresoles%' THEN 1 ELSE 0 END) AS TyresolesTagCount,
                    SUM(CASE WHEN Tags IS NOT NULL AND Tags <> '' AND Tags NOT LIKE '%Tyresoles%' THEN 1 ELSE 0 END) AS CleanTagsCount,
                    SUM(CASE WHEN SourceUrl IS NOT NULL AND SourceUrl <> '' THEN 1 ELSE 0 END) AS TotalWebLinkCount,
                    SUM(CASE WHEN (SourceUrl IS NOT NULL AND SourceUrl <> '') AND (Tags LIKE '%Tyresoles%' OR ModifiedBy IS NULL OR ModifiedBy NOT LIKE 'TagEnrich%') THEN 1 ELSE 0 END) AS PendingWebEnrichmentCount,
                    SUM(CASE WHEN ModifiedBy LIKE 'TagEnrich%' THEN 1 ELSE 0 END) AS EnrichedWebCount
                FROM CrmContact";

            using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                stats.TotalContacts = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0));
                stats.UnalignedStateCount = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1));
                stats.AlignedStateCount = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2));
                stats.TyresolesTagCount = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3));
                stats.CleanTagsCount = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetValue(4));
                stats.TotalWebLinkCount = reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader.GetValue(5));
                stats.PendingWebEnrichmentCount = reader.IsDBNull(6) ? 0 : Convert.ToInt32(reader.GetValue(6));
                stats.EnrichedWebCount = reader.IsDBNull(7) ? 0 : Convert.ToInt32(reader.GetValue(7));
            }
        }

        // Fetch recent enriched sample contacts
        stats.RecentEnrichedSamples = await _crmDb.CrmContacts
            .AsNoTracking()
            .Where(c => c.ModifiedBy != null && c.ModifiedBy.StartsWith("TagEnrich"))
            .OrderByDescending(c => c.ModifiedAt)
            .Take(8)
            .Select(c => new CrmContactSampleDto
            {
                Id = c.Id,
                FullName = c.FullName,
                State = c.State,
                Tags = c.Tags,
                SourceUrl = c.SourceUrl,
                ModifiedAt = c.ModifiedAt
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return stats;
    }

    public async Task<int> AlignStateCodesAsync(int? limit = null, CancellationToken ct = default)
    {
        var conn = _crmDb.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(ct).ConfigureAwait(false);

        int updatedTotal = 0;

        if (limit.HasValue && limit.Value > 0)
        {
            var contacts = await _crmDb.CrmContacts
                .Where(c => c.State != null && c.State.Trim().Length > 2)
                .Take(limit.Value)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            foreach (var c in contacts)
            {
                var norm = NormalizeState(c.State);
                if (!string.Equals(norm, c.State, StringComparison.Ordinal))
                {
                    c.State = norm;
                    c.ModifiedAt = DateTime.UtcNow;
                    c.ModifiedBy = c.ModifiedBy ?? "StateAlignment";
                    updatedTotal++;
                }
            }

            if (updatedTotal > 0)
                await _crmDb.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        else
        {
            // Ultra-fast bulk SQL alignment for all known state names
            foreach (var kvp in StateNameToCodeMap)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE CrmContact SET State = @code, ModifiedAt = SYSUTCDATETIME() WHERE State = @name";
                
                var pCode = cmd.CreateParameter();
                pCode.ParameterName = "@code";
                pCode.Value = kvp.Value;
                cmd.Parameters.Add(pCode);

                var pName = cmd.CreateParameter();
                pName.ParameterName = "@name";
                pName.Value = kvp.Key;
                cmd.Parameters.Add(pName);

                var rows = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                updatedTotal += rows;
            }

            // Also uppercase any remaining 2-letter lowercase codes
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "UPDATE CrmContact SET State = UPPER(LTRIM(RTRIM(State))) WHERE LEN(LTRIM(RTRIM(ISNULL(State, '')))) = 2 AND State <> UPPER(LTRIM(RTRIM(State)))";
                var rows = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                updatedTotal += rows;
            }
        }

        _logger.LogInformation("AlignStateCodesAsync: aligned {UpdatedCount} contacts.", updatedTotal);
        return updatedTotal;
    }

    public async Task<int> CleanUnwantedTagsAsync(int? limit = null, CancellationToken ct = default)
    {
        IQueryable<CrmContact> query = _crmDb.CrmContacts
            .Where(c => c.Tags != null && c.Tags.Contains("Tyresoles"));

        if (limit.HasValue && limit.Value > 0)
            query = query.Take(limit.Value);

        var contacts = await query.ToListAsync(ct).ConfigureAwait(false);
        int updated = 0;

        foreach (var c in contacts)
        {
            var cleanedList = CleanTagsList(c.Tags);
            var newTags = string.Join(", ", cleanedList);
            if (!string.Equals(newTags, c.Tags, StringComparison.Ordinal))
            {
                c.Tags = newTags;
                c.ModifiedAt = DateTime.UtcNow;
                c.ModifiedBy = c.ModifiedBy ?? "TagCleaner";
                updated++;
            }
        }

        if (updated > 0)
            await _crmDb.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("CleanUnwantedTagsAsync: cleaned {Count} contacts.", updated);
        return updated;
    }

    public async Task<CrmBatchEnrichmentResultDto> EnrichTagsFromWebBatchAsync(int batchSize, CancellationToken ct = default)
    {
        batchSize = Math.Clamp(batchSize, 1, 500);

        var result = new CrmBatchEnrichmentResultDto();

        // Find next contacts with web links that have not yet been enriched or still contain Tyresoles
        var contacts = await _crmDb.CrmContacts
            .Where(c => !string.IsNullOrEmpty(c.SourceUrl) &&
                        (c.Tags == null || c.Tags.Contains("Tyresoles") || c.ModifiedBy == null || !c.ModifiedBy.StartsWith("TagEnrich")))
            .OrderBy(c => c.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (contacts.Count == 0)
        {
            result.Success = true;
            result.Message = "No pending contacts found to enrich.";
            result.RemainingPendingCount = 0;
            return result;
        }

        var processedItems = new ConcurrentBag<CrmEnrichmentItemDto>();

        // Process scraping in parallel (bounded concurrency to respect target server)
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = 5,
            CancellationToken = ct
        };

        await Parallel.ForEachAsync(contacts, parallelOptions, async (contact, token) =>
        {
            var item = new CrmEnrichmentItemDto
            {
                Id = contact.Id,
                FullName = contact.FullName,
                OldTags = contact.Tags,
                SourceUrl = contact.SourceUrl
            };

            // 1. Align state code if needed
            var normState = NormalizeState(contact.State);
            if (!string.IsNullOrEmpty(normState))
            {
                contact.State = normState;
            }
            item.State = contact.State;

            // 2. Clean existing tags (strip Tyresoles)
            var currentTags = CleanTagsList(contact.Tags);

            // 3. Scrape features from web link
            List<string> scrapedFeatures = new();
            bool scrapeSuccess = false;
            string? scrapeError = null;

            if (!string.IsNullOrWhiteSpace(contact.SourceUrl))
            {
                try
                {
                    scrapedFeatures = await ScrapeFeaturesFromUrlAsync(contact.SourceUrl.Trim(), token).ConfigureAwait(false);
                    scrapeSuccess = true;
                }
                catch (Exception ex)
                {
                    scrapeError = ex.Message;
                    _logger.LogWarning(ex, "Failed to scrape features for contact {ContactId} from {Url}", contact.Id, contact.SourceUrl);
                }
            }

            item.FeaturesFound = scrapedFeatures;
            item.Success = scrapeSuccess;
            item.Error = scrapeError;

            // 4. Merge tags
            var merged = currentTags
                .Concat(scrapedFeatures)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var newTags = string.Join(", ", merged);
            contact.Tags = newTags;
            item.NewTags = newTags;

            contact.ModifiedAt = DateTime.UtcNow;
            contact.ModifiedBy = scrapedFeatures.Count > 0 ? "TagEnrichment" : (scrapeSuccess ? "TagEnrichment (NoFeatures)" : "TagEnrichment (ScrapeFailed)");

            processedItems.Add(item);
        }).ConfigureAwait(false);

        await _crmDb.SaveChangesAsync(ct).ConfigureAwait(false);

        result.ProcessedItems = processedItems.OrderBy(i => i.FullName).ToList();
        result.ProcessedCount = contacts.Count;
        result.ScrapedSuccessCount = result.ProcessedItems.Count(i => i.Success);
        result.ScrapedFailedCount = result.ProcessedItems.Count(i => !i.Success);
        result.TotalFeaturesAdded = result.ProcessedItems.Sum(i => i.FeaturesFound.Count);

        // Calculate remaining pending count
        result.RemainingPendingCount = await _crmDb.CrmContacts
            .CountAsync(c => !string.IsNullOrEmpty(c.SourceUrl) &&
                             (c.Tags == null || c.Tags.Contains("Tyresoles") || c.ModifiedBy == null || !c.ModifiedBy.StartsWith("TagEnrich")), ct)
            .ConfigureAwait(false);

        result.Success = true;
        result.Message = $"Enriched {result.ProcessedCount} contact(s). Added {result.TotalFeaturesAdded} feature tag(s). {result.RemainingPendingCount} pending.";

        return result;
    }

    private async Task<List<string>> ScrapeFeaturesFromUrlAsync(string url, CancellationToken ct)
    {
        var features = new List<string>();

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return features;

        HttpResponseMessage res;
        try
        {
            res = await _scraperClient.GetAsync(uri, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            // Brief fallback or timeout
            return features;
        }

        var html = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        // Check if hit bot protection challenge: document.cookie = "humans_21909=1"
        if (res.StatusCode == HttpStatusCode.Conflict || html.Contains("document.cookie", StringComparison.OrdinalIgnoreCase))
        {
            var cookieMatch = DocumentCookieRegex().Match(html);
            if (cookieMatch.Success)
            {
                var cookiePart = cookieMatch.Groups[1].Value.Split('=', 2);
                if (cookiePart.Length == 2)
                {
                    var cookieContainer = new CookieContainer();
                    cookieContainer.Add(uri, new Cookie(cookiePart[0].Trim(), cookiePart[1].Trim()));
                    using var subHandler = new SocketsHttpHandler { CookieContainer = cookieContainer, AutomaticDecompression = DecompressionMethods.All };
                    using var retryClient = new HttpClient(subHandler) { Timeout = TimeSpan.FromSeconds(10) };
                    retryClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                    
                    var retryRes = await retryClient.GetAsync(uri, ct).ConfigureAwait(false);
                    if (retryRes.IsSuccessStatusCode)
                    {
                        html = await retryRes.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    }
                }
            }
        }

        if (string.IsNullOrWhiteSpace(html))
            return features;

        // 1. Primary extractor for TransportFamily features:
        // Matches <a href=".../features/.../">... Feature Text ...</a>
        var matches = TransportFamilyFeatureLinkRegex().Matches(html);
        foreach (Match m in matches)
        {
            var rawText = m.Groups[1].Value;
            var cleanText = HtmlTagRegex().Replace(rawText, " ");
            cleanText = WebUtility.HtmlDecode(cleanText).Trim();
            cleanText = WhitespaceRegex().Replace(cleanText, " ");

            if (!string.IsNullOrWhiteSpace(cleanText) &&
                !features.Contains(cleanText, StringComparer.OrdinalIgnoreCase))
            {
                features.Add(cleanText);
            }
        }

        // 2. Generic fallback if no specific feature links found, check for general listing features or tags
        if (features.Count == 0 && html.Contains("Features", StringComparison.OrdinalIgnoreCase))
        {
            var ulMatch = FeaturesUlRegex().Match(html);
            if (ulMatch.Success)
            {
                var ulContent = ulMatch.Groups[1].Value;
                var liMatches = LiTagRegex().Matches(ulContent);
                foreach (Match lm in liMatches)
                {
                    var cleanText = HtmlTagRegex().Replace(lm.Groups[1].Value, " ");
                    cleanText = WebUtility.HtmlDecode(cleanText).Trim();
                    cleanText = WhitespaceRegex().Replace(cleanText, " ");
                    if (!string.IsNullOrWhiteSpace(cleanText) && cleanText.Length < 60 &&
                        !features.Contains(cleanText, StringComparer.OrdinalIgnoreCase))
                    {
                        features.Add(cleanText);
                    }
                }
            }
        }

        return features;
    }

    [GeneratedRegex(@"<a\s+[^>]*href=[""'][^""']*/features/[^""']*[""'][^>]*>(.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TransportFamilyFeatureLinkRegex();

    [GeneratedRegex(@"<ul[^>]*class=[""'][^""']*features[^""']*[""'][^>]*>(.*?)</ul>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex FeaturesUlRegex();

    [GeneratedRegex(@"<li[^>]*>(.*?)</li>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex LiTagRegex();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.Singleline)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"document\.cookie\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex DocumentCookieRegex();
}
