using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Tyresoles.Data.Features.Crm;
using Tyresoles.Data.Features.Crm.Entities;
using Tyresoles.Data.Features.Crm.Models;

namespace Tyresoles.Data.Features.Crm.Services;

public interface ILiveLeadDiscoveryService
{
    Task<List<DiscoveredLeadDto>> DiscoverLiveLeadsAsync(
        string division,
        string targetProduct,
        string city,
        string channel,
        int limit,
        CancellationToken ct = default);

    Task<List<DiscoveredLeadDto>> ScrapeLeadsFromWebUrlAsync(
        string url,
        string division,
        string targetProduct,
        int limit,
        CancellationToken ct = default);

    Task<CrawlCheckpointDto> GetCrawlCheckpointAsync(
        string url,
        CancellationToken ct = default);

    Task<CrawlPipelineResultDto> FetchWebUrlBatchAsync(
        string url,
        int pages,
        bool reset,
        string division,
        string targetProduct,
        CancellationToken ct = default);

    Task<CrawlPipelineResultDto> ProcessStagedLeadsAsync(
        string? url,
        int limit,
        bool dryRun,
        string? defaultLeadSourceType = null,
        string? defaultLeadSourceChannel = null,
        string? defaultRespCenter = null,
        CancellationToken ct = default);

    Task<CrawlCheckpointDto> ResetCrawlCheckpointAsync(
        string url,
        bool clearStaging,
        CancellationToken ct = default);

    Task<AutoExtractResultDto> AutoExtractWebLeadsAsync(
        string url,
        int pages,
        bool autoIngest,
        bool reset,
        string division,
        string targetProduct,
        bool dryRun,
        string? defaultLeadSourceType = null,
        string? defaultLeadSourceChannel = null,
        string? defaultRespCenter = null,
        CancellationToken ct = default);

    Task<List<StagedLeadItemDto>> GetStagedLeadsAsync(
        string? url,
        string? status,
        int limit,
        CancellationToken ct = default);
}

public class LiveLeadDiscoveryService : ILiveLeadDiscoveryService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LiveLeadDiscoveryService>? _logger;
    private readonly CrmDbContext _crmDb;

    public LiveLeadDiscoveryService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        CrmDbContext crmDb,
        ILogger<LiveLeadDiscoveryService>? logger = null)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _crmDb = crmDb;
        _logger = logger;
    }

    public async Task<List<DiscoveredLeadDto>> DiscoverLiveLeadsAsync(
        string division,
        string targetProduct,
        string city,
        string channel,
        int limit,
        CancellationToken ct = default)
    {
        var rawCity = (city ?? "Belgaum").Trim();
        if (rawCity.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || rawCity.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return await ScrapeLeadsFromWebUrlAsync(rawCity, division, targetProduct, limit, ct);
        }

        var radiusKm = 0;
        var radMatch = Regex.Match(rawCity, @"(?:within\s*|radius\s*|\b)(\d+)\s*km", RegexOptions.IgnoreCase);
        if (radMatch.Success && int.TryParse(radMatch.Groups[1].Value, out var parsedRad))
        {
            radiusKm = Math.Clamp(parsedRad, 5, 150);
        }

        var cleanCity = Regex.Replace(rawCity, @"\s*\([^)]*km[^)]*\)", "", RegexOptions.IgnoreCase);
        cleanCity = Regex.Replace(cleanCity, @"\s*(?:within\s*|radius\s*|\b)\d+\s*km.*$", "", RegexOptions.IgnoreCase).Trim(' ', ',', '-');
        if (string.IsNullOrWhiteSpace(cleanCity)) cleanCity = "Belgaum";

        var cleanProduct = (targetProduct ?? "Commercial Retreading").Trim();
        var cleanDivision = (division ?? "Tyresoles").Trim();
        var searchLimit = Math.Clamp(limit, 1, 100);

        // ─── Channel Gating: Non-Google-Maps channels MUST NOT fall through to Google Maps ───
        var isGoogleMapsChannel = string.IsNullOrWhiteSpace(channel)
            || channel.Equals("Google-Maps", StringComparison.OrdinalIgnoreCase);

        // 1. Check Python Multi-Source Harvester (LinkedIn OSINT, Google Maps, OEM Dealers, Transport Directory)
        try
        {
            var pythonLeads = await RunPythonHarvesterAsync(cleanCity, cleanProduct, cleanDivision, channel, searchLimit, radiusKm, ct);
            if (pythonLeads.Count > 0)
            {
                return pythonLeads.Take(searchLimit).ToList();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Python Harvester execution failed for {Channel} in {City}", channel, cleanCity);
        }

        // ─── If user explicitly selected a non-Google-Maps channel, do NOT fall through ───
        // Return an empty list so frontend cleanly displays the zero-results state without creating dummy CRM leads
        if (!isGoogleMapsChannel)
        {
            _logger?.LogInformation("Channel {Channel} returned 0 results for {City}/{Product}. NOT falling through to Google Maps.", channel, cleanCity, cleanProduct);
            return new List<DiscoveredLeadDto>();
        }

        // 2. Check for Google Places API Key (If user provided one)
        var googleApiKey = _configuration["LeadHarvester:GooglePlacesApiKey"]
            ?? _configuration["GooglePlaces:ApiKey"]
            ?? Environment.GetEnvironmentVariable("GOOGLE_PLACES_API_KEY")
            ?? Environment.GetEnvironmentVariable("GOOGLE_MAPS_API_KEY");

        if (!string.IsNullOrWhiteSpace(googleApiKey) && !googleApiKey.Contains("YOUR_"))
        {
            try
            {
                var googleLeads = await FetchGooglePlacesNewAsync(cleanCity, cleanProduct, cleanDivision, searchLimit, radiusKm, googleApiKey, ct);
                if (googleLeads.Count > 0)
                {
                    return googleLeads.Take(searchLimit).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to query Google Places API for {City} / {Product}", cleanCity, cleanProduct);
            }
        }

        // 3. Direct Google Maps Free Scraper (Zero-Key Local Browser Engine)
        try
        {
            var scrapedLeads = await ScrapeGoogleMapsDirectAsync(cleanCity, cleanProduct, cleanDivision, searchLimit, radiusKm, ct);
            if (scrapedLeads.Count > 0)
            {
                return scrapedLeads.Take(searchLimit).ToList();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Direct Google Maps scraper failed for {City} / {Product}", cleanCity, cleanProduct);
        }

        // 4. Check for SerpApi Google Maps Engine (Secondary Provider)
        var serpApiKey = _configuration["LeadHarvester:SerpApiKey"]
            ?? _configuration["SerpApi:ApiKey"]
            ?? Environment.GetEnvironmentVariable("SERP_API_KEY");

        if (!string.IsNullOrWhiteSpace(serpApiKey) && !serpApiKey.Contains("YOUR_"))
        {
            try
            {
                var serpLeads = await FetchSerpApiGoogleMapsAsync(cleanCity, cleanProduct, cleanDivision, searchLimit, serpApiKey, ct);
                if (serpLeads.Count > 0)
                {
                    return serpLeads.Take(searchLimit).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to query SerpApi Google Maps for {City} / {Product}", cleanCity, cleanProduct);
            }
        }

        // 4. Live OpenStreetMap Geospatial Directory
        try
        {
            var osmLeads = await FetchOsmGeospatialLeadsAsync(cleanCity, cleanProduct, cleanDivision, channel, searchLimit, radiusKm, ct);
            if (osmLeads.Count > 0)
            {
                return osmLeads.Take(searchLimit).ToList();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "OSM query failed for {City} / {Product}", cleanCity, cleanProduct);
        }

        return BuildLiveSearchCardLead(cleanCity, cleanProduct, cleanDivision, radiusKm);
    }

    /// <summary>
    /// Direct Google Maps Scraper: launches Chrome/Edge in headless mode via CDP,
    /// loads live Google Maps search results, and parses 100% real business cards with real phone numbers and addresses.
    /// </summary>
    private async Task<List<DiscoveredLeadDto>> ScrapeGoogleMapsDirectAsync(
        string city,
        string product,
        string division,
        int maxCount,
        int radiusKm,
        CancellationToken ct)
    {
        var results = new List<DiscoveredLeadDto>();
        var query = BuildSearchQuery(product, city);

        // Locate google_maps_scraper.cjs script
        var possiblePaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "google_maps_scraper.cjs"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "google_maps_scraper.cjs"),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "back-end", "Tyresoles.Data", "Tools", "google_maps_scraper.cjs")),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Tyresoles.Data", "Tools", "google_maps_scraper.cjs")),
            @"d:\Work Desk\Tyresoles\back-end\Tyresoles.Data\Tools\google_maps_scraper.cjs"
        };

        var scriptPath = possiblePaths.FirstOrDefault(File.Exists);
        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            _logger?.LogWarning("google_maps_scraper.cjs script not found in {Paths}", string.Join(", ", possiblePaths));
            return results;
        }

        var psi = new ProcessStartInfo
        {
            FileName = "node",
            Arguments = $"\"{scriptPath}\" \"{query}\" {maxCount} \"{city}\" {radiusKm}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = new Process { StartInfo = psi };
        proc.Start();

        var outputTask = proc.StandardOutput.ReadToEndAsync(ct);
        var errorTask = proc.StandardError.ReadToEndAsync(ct);

        await proc.WaitForExitAsync(ct).ConfigureAwait(false);
        var json = await outputTask.ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(json) && json.TrimStart().StartsWith("["))
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var title = item.TryGetProperty("title", out var tProp) ? tProp.GetString()?.Trim() ?? "" : "";
                if (string.IsNullOrWhiteSpace(title)) continue;

                var rawPhone = item.TryGetProperty("phone", out var pProp) ? pProp.GetString() ?? "" : "";
                var rawAddress = item.TryGetProperty("address", out var aProp) ? aProp.GetString() ?? "" : "";

                var cleanAddress = !string.IsNullOrWhiteSpace(rawAddress)
                    ? (rawAddress.Contains(city, StringComparison.OrdinalIgnoreCase) ? rawAddress : $"{rawAddress}, {city}")
                    : $"{title}, {city}, India";

                var mapUrl = item.TryGetProperty("url", out var uProp) ? uProp.GetString() ?? "" : $"https://www.google.com/maps/search/{Uri.EscapeDataString(title + " " + city)}";
                var website = item.TryGetProperty("website", out var wProp) ? wProp.GetString() : null;
                var ratingStr = item.TryGetProperty("rating", out var rProp) ? rProp.GetString() ?? "" : "";
                var reviewStr = item.TryGetProperty("reviews", out var revProp) ? revProp.GetString() ?? "" : "";
                var snippet = item.TryGetProperty("snippet", out var snProp) ? snProp.GetString() ?? "" : "";

                var locStr = item.TryGetProperty("location", out var locProp) ? locProp.GetString() : null;
                if (string.IsNullOrWhiteSpace(locStr))
                {
                    locStr = GetCityCoordinates(city, cleanAddress);
                }

                decimal? distKm = null;
                if (item.TryGetProperty("distanceKm", out var distProp) && distProp.ValueKind == JsonValueKind.Number)
                {
                    distKm = distProp.GetDecimal();
                }

                decimal rating = 0;
                var match = Regex.Match(ratingStr, @"\d+(\.\d+)?");
                if (match.Success) decimal.TryParse(match.Value, out rating);

                var score = 0.88m;
                if (!string.IsNullOrWhiteSpace(rawPhone)) score += 0.08m;
                if (rating > 4.0m) score += 0.03m;
                score = Math.Min(0.99m, score);

                var distTag = (distKm.HasValue && radiusKm > 0) ? $", Distance:{distKm:F1}km" : "";
                var distSnippetPrefix = (distKm.HasValue && radiusKm > 0) ? $"📍 {distKm:F1} km from {city} center • " : "";

                results.Add(new DiscoveredLeadDto
                {
                    FullName = "Branch Manager / Business Head",
                    CompanyName = title,
                    MobileNo = rawPhone,
                    Address = cleanAddress,
                    City = city,
                    State = ResolveState(city, cleanAddress),
                    Division = division,
                    TargetProduct = product,
                    LeadSourceType = "Automated",
                    LeadSourceChannel = "Google-Maps",
                    SourceUrl = mapUrl,
                    Website = string.IsNullOrWhiteSpace(website) ? null : website,
                    QualityScore = score,
                    ScrapingQuery = radiusKm > 0 ? $"{query} (±{radiusKm}km)" : query,
                    Tags = radiusKm > 0
                        ? $"Google-Maps-Live, Rating:{rating:F1}, Radius:{radiusKm}km{distTag}, Division:{division}, Product:{product}, City:{city}"
                        : $"Google-Maps-Live, Rating:{rating:F1}, Division:{division}, Product:{product}, City:{city}",
                    Snippet = distSnippetPrefix + (string.IsNullOrWhiteSpace(snippet) ? $"Verified business in {city} via Google Maps" : snippet),
                    ContactType = "Lead",
                    Location = locStr
                });

                if (results.Count >= maxCount) break;
            }
        }

        return results;
    }

    private async Task<List<DiscoveredLeadDto>> FetchGooglePlacesNewAsync(
        string city,
        string product,
        string division,
        int maxCount,
        int radiusKm,
        string apiKey,
        CancellationToken ct)
    {
        var results = new List<DiscoveredLeadDto>();
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(15);

        var query = BuildSearchQuery(product, city);
        var requestUrl = "https://places.googleapis.com/v1/places:searchText";

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        request.Headers.Add("X-Goog-Api-Key", apiKey);
        request.Headers.Add("X-Goog-FieldMask", "places.id,places.displayName,places.formattedAddress,places.nationalPhoneNumber,places.internationalPhoneNumber,places.googleMapsUri,places.websiteUri,places.rating,places.userRatingCount,places.businessStatus,places.location");

        object payload;
        var cityCoords = GetCityCoordinates(city);
        if (!string.IsNullOrWhiteSpace(cityCoords) && cityCoords.Contains(','))
        {
            var parts = cityCoords.Split(',');
            if (double.TryParse(parts[0].Trim(), out var cLat) && double.TryParse(parts[1].Trim(), out var cLng))
            {
                payload = new
                {
                    textQuery = $"{query} within {radiusKm} km",
                    pageSize = Math.Min(maxCount, 20),
                    locationRestriction = new
                    {
                        circle = new
                        {
                            center = new { latitude = cLat, longitude = cLng },
                            radius = Math.Min(radiusKm * 1000.0, 50000.0)
                        }
                    }
                };
            }
            else
            {
                payload = new { textQuery = $"{query} within {radiusKm} km", pageSize = Math.Min(maxCount, 20) };
            }
        }
        else
        {
            payload = new { textQuery = $"{query} within {radiusKm} km", pageSize = Math.Min(maxCount, 20) };
        }

        request.Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            _logger?.LogWarning("Google Places API error ({Status}): {Error}", response.StatusCode, err);
            return results;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("places", out var placesElement))
        {
            return results;
        }

        foreach (var place in placesElement.EnumerateArray())
        {
            var name = place.TryGetProperty("displayName", out var dn) && dn.TryGetProperty("text", out var t)
                ? t.GetString()?.Trim() ?? ""
                : "";

            if (string.IsNullOrWhiteSpace(name)) continue;

            var address = place.TryGetProperty("formattedAddress", out var addrProp) ? addrProp.GetString() ?? "" : $"{city}, India";
            
            var phone = place.TryGetProperty("nationalPhoneNumber", out var natPhone) && !string.IsNullOrWhiteSpace(natPhone.GetString())
                ? natPhone.GetString()!
                : (place.TryGetProperty("internationalPhoneNumber", out var intlPhone) ? intlPhone.GetString() ?? "" : "");

            var mapUri = place.TryGetProperty("googleMapsUri", out var uriProp) ? uriProp.GetString() ?? "" : $"https://www.google.com/maps/search/{Uri.EscapeDataString(name + " " + city)}";
            var website = place.TryGetProperty("websiteUri", out var webProp) ? webProp.GetString() : null;
            
            var rating = place.TryGetProperty("rating", out var ratProp) ? ratProp.GetDecimal() : 0m;
            var userRatingsTotal = place.TryGetProperty("userRatingCount", out var urcProp) ? urcProp.GetInt32() : 0;

            string? locationCoords = null;
            if (place.TryGetProperty("location", out var locProp))
            {
                if (locProp.TryGetProperty("latitude", out var latP) && locProp.TryGetProperty("longitude", out var lngP))
                {
                    if (latP.TryGetDouble(out var dLat) && lngP.TryGetDouble(out var dLng))
                    {
                        locationCoords = $"{dLat:F6}, {dLng:F6}";
                    }
                }
            }

            var score = 0.85m;
            if (!string.IsNullOrWhiteSpace(phone)) score += 0.08m;
            if (rating > 4.0m) score += 0.05m;
            score = Math.Min(0.99m, score);

            var snippet = rating > 0
                ? $"⭐ {rating:F1} ({userRatingsTotal} reviews on Google Maps) • {address}"
                : $"Verified Google Maps listing at {address}";

            results.Add(new DiscoveredLeadDto
            {
                FullName = "Branch Manager / Business Contact",
                CompanyName = name,
                MobileNo = phone,
                Address = address,
                City = city,
                State = ResolveState(city, address),
                Division = division,
                TargetProduct = product,
                LeadSourceType = "Automated",
                LeadSourceChannel = "Google-Maps",
                SourceUrl = mapUri,
                Website = website,
                QualityScore = score,
                ScrapingQuery = query,
                Tags = $"Google-Places-Verified, Rating:{rating:F1}, Division:{division}, Product:{product}, City:{city}",
                Snippet = snippet,
                ContactType = "Lead",
                Location = locationCoords
            });
        }

        return results;
    }

    private async Task<List<DiscoveredLeadDto>> FetchSerpApiGoogleMapsAsync(
        string city,
        string product,
        string division,
        int maxCount,
        string apiKey,
        CancellationToken ct)
    {
        var results = new List<DiscoveredLeadDto>();
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(15);

        var query = BuildSearchQuery(product, city);
        var url = $"https://serpapi.com/search.json?engine=google_maps&q={Uri.EscapeDataString(query)}&api_key={apiKey}&num={maxCount}";

        var response = await client.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return results;

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("local_results", out var localResults))
        {
            return results;
        }

        foreach (var item in localResults.EnumerateArray())
        {
            var title = item.TryGetProperty("title", out var titleProp) ? titleProp.GetString()?.Trim() ?? "" : "";
            if (string.IsNullOrWhiteSpace(title)) continue;

            var address = item.TryGetProperty("address", out var addrProp) ? addrProp.GetString() ?? "" : $"{city}, India";
            var phone = item.TryGetProperty("phone", out var phoneProp) ? phoneProp.GetString() ?? "" : "";
            var link = item.TryGetProperty("link", out var linkProp) ? linkProp.GetString() ?? "" : $"https://www.google.com/maps/search/{Uri.EscapeDataString(title + " " + city)}";
            var website = item.TryGetProperty("website", out var webProp) ? webProp.GetString() : null;
            var rating = item.TryGetProperty("rating", out var ratProp) ? ratProp.GetDecimal() : 0m;
            var reviews = item.TryGetProperty("reviews", out var revProp) ? revProp.GetInt32() : 0;

            var score = 0.85m;
            if (!string.IsNullOrWhiteSpace(phone)) score += 0.08m;
            if (rating > 4.0m) score += 0.05m;
            score = Math.Min(0.99m, score);

            results.Add(new DiscoveredLeadDto
            {
                FullName = "Business Contact",
                CompanyName = title,
                MobileNo = phone,
                Address = address,
                City = city,
                State = ResolveState(city, address),
                Division = division,
                TargetProduct = product,
                LeadSourceType = "Automated",
                LeadSourceChannel = "Google-Maps",
                SourceUrl = link,
                Website = website,
                QualityScore = score,
                ScrapingQuery = query,
                Tags = $"SerpApi-GoogleMaps, Rating:{rating:F1}, Division:{division}, Product:{product}, City:{city}",
                Snippet = $"⭐ {rating:F1} ({reviews} reviews) • {address}"
            });
        }

        return results;
    }

    private async Task<List<DiscoveredLeadDto>> FetchOsmGeospatialLeadsAsync(
        string city,
        string product,
        string division,
        string channel,
        int maxCount,
        int radiusKm,
        CancellationToken ct)
    {
        var results = new List<DiscoveredLeadDto>();
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);

        var geoUrl = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(city + ", India")}&format=json&limit=1";
        using var geoReq = new HttpRequestMessage(HttpMethod.Get, geoUrl);
        geoReq.Headers.UserAgent.ParseAdd("TyresolesLeadHarvester/2.0 (crm@tyresoles.com)");

        var geoResp = await client.SendAsync(geoReq, ct);
        if (!geoResp.IsSuccessStatusCode) return results;

        var geoJson = await geoResp.Content.ReadAsStringAsync(ct);
        using var geoDoc = JsonDocument.Parse(geoJson);
        if (geoDoc.RootElement.GetArrayLength() == 0) return results;

        var first = geoDoc.RootElement[0];
        if (!first.TryGetProperty("lat", out var latProp) || !first.TryGetProperty("lon", out var lonProp)) return results;
        if (!double.TryParse(latProp.GetString(), out var lat) || !double.TryParse(lonProp.GetString(), out var lon)) return results;

        var amenityTag = GetOsmTagForProduct(product);
        var overpassUrl = "https://overpass-api.de/api/interpreter";
        var latDelta = Math.Max(0.05, radiusKm / 111.0);
        var lonDelta = Math.Max(0.05, radiusKm / (111.0 * Math.Cos(lat * Math.PI / 180.0)));
        var ql = $@"
        [out:json][timeout:10];
        (
          node[{amenityTag}]({lat - latDelta},{lon - lonDelta},{lat + latDelta},{lon + lonDelta});
          way[{amenityTag}]({lat - latDelta},{lon - lonDelta},{lat + latDelta},{lon + lonDelta});
        );
        out center {maxCount};
        ";

        using var opReq = new HttpRequestMessage(HttpMethod.Post, overpassUrl);
        opReq.Content = new FormUrlEncodedContent(new Dictionary<string, string> { { "data", ql } });
        opReq.Headers.UserAgent.ParseAdd("TyresolesLeadHarvester/2.0");

        var opResp = await client.SendAsync(opReq, ct);
        if (!opResp.IsSuccessStatusCode) return results;

        var opJson = await opResp.Content.ReadAsStringAsync(ct);
        using var opDoc = JsonDocument.Parse(opJson);

        if (opDoc.RootElement.TryGetProperty("elements", out var elements))
        {
            foreach (var el in elements.EnumerateArray())
            {
                if (!el.TryGetProperty("tags", out var tags)) continue;
                if (!tags.TryGetProperty("name", out var nameProp)) continue;

                var name = nameProp.GetString()?.Trim();
                if (string.IsNullOrWhiteSpace(name) || name.Length < 3) continue;

                var street = tags.TryGetProperty("addr:street", out var stProp) ? stProp.GetString() : "";
                var phone = tags.TryGetProperty("phone", out var phProp) ? phProp.GetString() :
                            (tags.TryGetProperty("contact:phone", out var cphProp) ? cphProp.GetString() : "");

                var cleanMobile = !string.IsNullOrWhiteSpace(phone) ? phone : "";
                var address = !string.IsNullOrWhiteSpace(street)
                    ? $"{street}, {city}"
                    : $"Commercial Area, {city}";

                var directMapUrl = $"https://www.google.com/maps/search/{Uri.EscapeDataString(name + " " + city)}";

                string? osmCoords = null;
                if (el.TryGetProperty("lat", out var latE) && el.TryGetProperty("lon", out var lonE))
                {
                    if (latE.TryGetDouble(out var dLat) && lonE.TryGetDouble(out var dLon))
                        osmCoords = $"{dLat:F6}, {dLon:F6}";
                }
                else if (el.TryGetProperty("center", out var centerE) && centerE.TryGetProperty("lat", out var cLat) && centerE.TryGetProperty("lon", out var cLon))
                {
                    if (cLat.TryGetDouble(out var dLat) && cLon.TryGetDouble(out var dLon))
                        osmCoords = $"{dLat:F6}, {dLon:F6}";
                }
                if (string.IsNullOrWhiteSpace(osmCoords))
                {
                    osmCoords = $"{lat:F6}, {lon:F6}";
                }

                results.Add(new DiscoveredLeadDto
                {
                    FullName = "Local Contact",
                    CompanyName = name,
                    MobileNo = cleanMobile,
                    Address = address,
                    City = city,
                    State = ResolveState(city, address),
                    Division = division,
                    TargetProduct = product,
                    LeadSourceType = "Automated",
                    LeadSourceChannel = channel ?? "OpenStreetMap",
                    SourceUrl = directMapUrl,
                    QualityScore = 0.88m,
                    ScrapingQuery = $"{product} in {city} (±{radiusKm}km)",
                    Tags = $"OpenStreetMap, Radius:{radiusKm}km, Division:{division}, Product:{product}, City:{city}",
                    Snippet = $"Verified physical entity in {city} (within {radiusKm}km radius) via OpenStreetMap database",
                    ContactType = "Lead",
                    Location = osmCoords
                });
            }
        }

        return results;
    }

    private List<DiscoveredLeadDto> BuildLiveSearchCardLead(string city, string product, string division, int radiusKm = 0)
    {
        var cleanQuery = radiusKm > 0 ? $"{BuildSearchQuery(product, city)} within {radiusKm} km" : BuildSearchQuery(product, city);
        var coords = GetCityCoordinates(city);
        string mapSearchUrl;
        if (radiusKm > 0 && !string.IsNullOrWhiteSpace(coords) && coords.Contains(','))
        {
            var parts = coords.Split(',');
            var zoom = radiusKm <= 10 ? 12 : (radiusKm <= 25 ? 11 : (radiusKm <= 50 ? 10 : 9));
            mapSearchUrl = $"https://www.google.com/maps/search/{Uri.EscapeDataString(cleanQuery)}/@{parts[0].Trim()},{parts[1].Trim()},{zoom}z";
        }
        else
        {
            mapSearchUrl = $"https://www.google.com/maps/search/{Uri.EscapeDataString(cleanQuery)}";
        }

        return new List<DiscoveredLeadDto>
        {
            new DiscoveredLeadDto
            {
                FullName = "Google Maps Live Query",
                CompanyName = radiusKm > 0 ? $"Live Google Maps Search: {product} in {city} (±{radiusKm} km)" : $"Live Google Maps Search: {product} in {city}",
                MobileNo = "View on Maps",
                Address = radiusKm > 0 ? $"Direct query covering {city} and surrounding industrial hubs within {radiusKm} km radius." : $"Direct query for {city}.",
                City = city,
                State = ResolveState(city, null),
                Division = division,
                TargetProduct = product,
                LeadSourceType = "Automated",
                LeadSourceChannel = "Google-Maps",
                SourceUrl = mapSearchUrl,
                QualityScore = 0.90m,
                ScrapingQuery = cleanQuery,
                Tags = $"Google-Maps-Search, Radius:{radiusKm}km",
                Snippet = $"Click 'View on Google Maps' to view all real matching business listings directly on Google Maps across a {radiusKm} km radius.",
                ContactType = "Lead",
                Location = coords
            }
        };
    }

    private string BuildSearchQuery(string product, string city)
    {
        var p = product.ToLowerInvariant();
        if (p.Contains("commercial retread") || p.Contains("fleet") || p.Contains("pre-cure"))
            return $"Transporters logistics fleet companies in {city}";
        if (p.Contains("radial") || p.Contains("tubeless"))
            return $"Interstate cargo transport companies in {city}";
        if (p.Contains("otr") || p.Contains("mining") || p.Contains("quarry"))
            return $"Stone quarry mining tipper earthmoving contractors in {city}";
        if (p.Contains("rrt") || p.Contains("dealer") || p.Contains("tyre"))
            return $"Commercial truck tyre dealers in {city}";
        if (p.Contains("gym") || p.Contains("tuffloor") || p.Contains("fitness"))
            return $"Gym and fitness centers in {city}";
        if (p.Contains("playsafe") || p.Contains("school") || p.Contains("playground"))
            return $"Pre schools and international schools in {city}";
        if (p.Contains("sports") || p.Contains("badminton") || p.Contains("herculan"))
            return $"Badminton academy and sports turf in {city}";
        if (p.Contains("runathon") || p.Contains("track"))
            return $"Sports stadiums and residential townships in {city}";

        return $"{product} in {city}";
    }

    private string GetOsmTagForProduct(string product)
    {
        var p = product.ToLowerInvariant();
        if (p.Contains("gym") || p.Contains("tuffloor")) return "\"leisure\"=\"fitness_centre\"";
        if (p.Contains("playsafe") || p.Contains("school")) return "\"amenity\"=\"school\"";
        if (p.Contains("sports") || p.Contains("badminton")) return "\"leisure\"=\"sports_centre\"";
        if (p.Contains("quarry") || p.Contains("mining")) return "\"landuse\"=\"quarry\"";
        return "\"amenity\"=\"truck_stop\"";
    }

    private static readonly Dictionary<string, string> CityToState = new(StringComparer.OrdinalIgnoreCase)
    {
        // Maharashtra
        ["pune"] = "Maharashtra",
        ["mumbai"] = "Maharashtra",
        ["bhiwandi"] = "Maharashtra",
        ["thane"] = "Maharashtra",
        ["navi mumbai"] = "Maharashtra",
        ["nagpur"] = "Maharashtra",
        ["nashik"] = "Maharashtra",
        ["aurangabad"] = "Maharashtra",
        ["chhatrapati sambhajinagar"] = "Maharashtra",
        ["solapur"] = "Maharashtra",
        ["kolhapur"] = "Maharashtra",
        ["amravati"] = "Maharashtra",
        ["nanded"] = "Maharashtra",
        ["sangli"] = "Maharashtra",
        ["jalgaon"] = "Maharashtra",
        ["akola"] = "Maharashtra",
        ["latur"] = "Maharashtra",
        ["dhule"] = "Maharashtra",
        ["ahmednagar"] = "Maharashtra",
        ["chandrapur"] = "Maharashtra",
        ["parbhani"] = "Maharashtra",
        ["panvel"] = "Maharashtra",
        ["satara"] = "Maharashtra",
        ["ratnagiri"] = "Maharashtra",
        ["sindhudurg"] = "Maharashtra",
        ["wardha"] = "Maharashtra",
        ["yavatmal"] = "Maharashtra",
        ["gondia"] = "Maharashtra",
        ["gadchiroli"] = "Maharashtra",
        ["hingoli"] = "Maharashtra",
        ["osmanabad"] = "Maharashtra",
        ["dharashiv"] = "Maharashtra",
        ["beed"] = "Maharashtra",
        ["palghar"] = "Maharashtra",
        ["raigad"] = "Maharashtra",

        // Karnataka
        ["belgaum"] = "Karnataka",
        ["belagavi"] = "Karnataka",
        ["bangalore"] = "Karnataka",
        ["bengaluru"] = "Karnataka",
        ["hubli"] = "Karnataka",
        ["dharwad"] = "Karnataka",
        ["hubballi"] = "Karnataka",
        ["mangalore"] = "Karnataka",
        ["mangaluru"] = "Karnataka",
        ["mysore"] = "Karnataka",
        ["mysuru"] = "Karnataka",
        ["gulbarga"] = "Karnataka",
        ["kalaburagi"] = "Karnataka",
        ["bellary"] = "Karnataka",
        ["ballari"] = "Karnataka",
        ["davangere"] = "Karnataka",
        ["shimoga"] = "Karnataka",
        ["shivamogga"] = "Karnataka",
        ["tumkur"] = "Karnataka",
        ["tumakuru"] = "Karnataka",
        ["raichur"] = "Karnataka",
        ["bidar"] = "Karnataka",
        ["hospet"] = "Karnataka",
        ["hosapete"] = "Karnataka",
        ["bijapur"] = "Karnataka",
        ["vijayapura"] = "Karnataka",
        ["udupi"] = "Karnataka",
        ["karwar"] = "Karnataka",
        ["chikmagalur"] = "Karnataka",
        ["hassan"] = "Karnataka",
        ["chitradurga"] = "Karnataka",
        ["kolar"] = "Karnataka",
        ["mandya"] = "Karnataka",
        ["bagalkot"] = "Karnataka",

        // Gujarat
        ["ahmedabad"] = "Gujarat",
        ["surat"] = "Gujarat",
        ["vadodara"] = "Gujarat",
        ["baroda"] = "Gujarat",
        ["rajkot"] = "Gujarat",
        ["bhavnagar"] = "Gujarat",
        ["jamnagar"] = "Gujarat",
        ["junagadh"] = "Gujarat",
        ["gandhinagar"] = "Gujarat",
        ["anand"] = "Gujarat",
        ["navsari"] = "Gujarat",
        ["morbi"] = "Gujarat",
        ["nadiad"] = "Gujarat",
        ["surendranagar"] = "Gujarat",
        ["bharuch"] = "Gujarat",
        ["mehsana"] = "Gujarat",
        ["bhuj"] = "Gujarat",
        ["porbandar"] = "Gujarat",
        ["vapi"] = "Gujarat",
        ["valsad"] = "Gujarat",
        ["gandhidham"] = "Gujarat",
        ["ankleshwar"] = "Gujarat",

        // Telangana
        ["hyderabad"] = "Telangana",
        ["secunderabad"] = "Telangana",
        ["warangal"] = "Telangana",
        ["nizamabad"] = "Telangana",
        ["khammam"] = "Telangana",
        ["karimnagar"] = "Telangana",
        ["ramagundam"] = "Telangana",
        ["mahbubnagar"] = "Telangana",
        ["nalgonda"] = "Telangana",
        ["adilabad"] = "Telangana",

        // Andhra Pradesh
        ["visakhapatnam"] = "Andhra Pradesh",
        ["vizag"] = "Andhra Pradesh",
        ["vijayawada"] = "Andhra Pradesh",
        ["guntur"] = "Andhra Pradesh",
        ["nellore"] = "Andhra Pradesh",
        ["kurnool"] = "Andhra Pradesh",
        ["rajahmundry"] = "Andhra Pradesh",
        ["tirupati"] = "Andhra Pradesh",
        ["kakinada"] = "Andhra Pradesh",
        ["kadapa"] = "Andhra Pradesh",
        ["anantapur"] = "Andhra Pradesh",
        ["vizianagaram"] = "Andhra Pradesh",
        ["eluru"] = "Andhra Pradesh",
        ["ongole"] = "Andhra Pradesh",

        // Tamil Nadu
        ["chennai"] = "Tamil Nadu",
        ["coimbatore"] = "Tamil Nadu",
        ["madurai"] = "Tamil Nadu",
        ["tiruchirappalli"] = "Tamil Nadu",
        ["trichy"] = "Tamil Nadu",
        ["salem"] = "Tamil Nadu",
        ["tirunelveli"] = "Tamil Nadu",
        ["tiruppur"] = "Tamil Nadu",
        ["erode"] = "Tamil Nadu",
        ["vellore"] = "Tamil Nadu",
        ["thoothukudi"] = "Tamil Nadu",
        ["dindigul"] = "Tamil Nadu",
        ["thanjavur"] = "Tamil Nadu",
        ["hosur"] = "Tamil Nadu",
        ["ranipet"] = "Tamil Nadu",
        ["kanchipuram"] = "Tamil Nadu",

        // Goa
        ["goa"] = "Goa",
        ["panaji"] = "Goa",
        ["margao"] = "Goa",
        ["vasco"] = "Goa",
        ["mapusa"] = "Goa",
        ["ponda"] = "Goa",

        // Rajasthan
        ["jaipur"] = "Rajasthan",
        ["jodhpur"] = "Rajasthan",
        ["kota"] = "Rajasthan",
        ["bikaner"] = "Rajasthan",
        ["ajmer"] = "Rajasthan",
        ["udaipur"] = "Rajasthan",
        ["bhilwara"] = "Rajasthan",
        ["alwar"] = "Rajasthan",

        // Madhya Pradesh
        ["indore"] = "Madhya Pradesh",
        ["bhopal"] = "Madhya Pradesh",
        ["jabalpur"] = "Madhya Pradesh",
        ["gwalior"] = "Madhya Pradesh",
        ["ujjain"] = "Madhya Pradesh",

        // Uttar Pradesh
        ["lucknow"] = "Uttar Pradesh",
        ["kanpur"] = "Uttar Pradesh",
        ["ghaziabad"] = "Uttar Pradesh",
        ["agra"] = "Uttar Pradesh",
        ["meerut"] = "Uttar Pradesh",
        ["varanasi"] = "Uttar Pradesh",
        ["noida"] = "Uttar Pradesh",
        ["greater noida"] = "Uttar Pradesh",

        // Delhi
        ["delhi"] = "Delhi",
        ["new delhi"] = "Delhi",

        // Punjab & Haryana & Chandigarh
        ["chandigarh"] = "Chandigarh",
        ["ludhiana"] = "Punjab",
        ["amritsar"] = "Punjab",
        ["jalandhar"] = "Punjab",
        ["gurugram"] = "Haryana",
        ["gurgaon"] = "Haryana",
        ["faridabad"] = "Haryana",

        // West Bengal
        ["kolkata"] = "West Bengal",
        ["howrah"] = "West Bengal",
        ["durgapur"] = "West Bengal",
        ["asansol"] = "West Bengal",

        // Kerala
        ["kochi"] = "Kerala",
        ["cochin"] = "Kerala",
        ["thiruvananthapuram"] = "Kerala",
        ["kozhikode"] = "Kerala",

        // Bihar & Jharkhand
        ["patna"] = "Bihar",
        ["ranchi"] = "Jharkhand",
        ["jamshedpur"] = "Jharkhand",

        // Odisha & Chhattisgarh
        ["bhubaneswar"] = "Odisha",
        ["cuttack"] = "Odisha",
        ["raipur"] = "Chhattisgarh",

        // Assam & Uttarakhand & Himachal
        ["guwahati"] = "Assam",
        ["dehradun"] = "Uttarakhand",
        ["shimla"] = "Himachal Pradesh"
    };

    private static readonly string[] IndianStates = new[]
    {
        "Andhra Pradesh", "Arunachal Pradesh", "Assam", "Bihar", "Chhattisgarh",
        "Goa", "Gujarat", "Haryana", "Himachal Pradesh", "Jharkhand",
        "Karnataka", "Kerala", "Madhya Pradesh", "Maharashtra", "Manipur",
        "Meghalaya", "Mizoram", "Nagaland", "Odisha", "Punjab",
        "Rajasthan", "Sikkim", "Tamil Nadu", "Telangana", "Tripura",
        "Uttar Pradesh", "Uttarakhand", "West Bengal", "Delhi", "Chandigarh",
        "Puducherry", "Jammu and Kashmir", "Ladakh"
    };

    public static string? ResolveState(string? city, string? address)
    {
        // 1. Direct city lookup
        if (!string.IsNullOrWhiteSpace(city))
        {
            var cleanCity = city.Trim();
            if (CityToState.TryGetValue(cleanCity, out var stateFromCity))
            {
                return stateFromCity;
            }
        }

        // 2. Scan address for exact state name
        if (!string.IsNullOrWhiteSpace(address))
        {
            foreach (var state in IndianStates)
            {
                if (Regex.IsMatch(address, $@"\b{Regex.Escape(state)}\b", RegexOptions.IgnoreCase))
                {
                    return state;
                }
            }

            // 3. Scan address for any recognized city
            foreach (var kvp in CityToState)
            {
                if (Regex.IsMatch(address, $@"\b{Regex.Escape(kvp.Key)}\b", RegexOptions.IgnoreCase))
                {
                    return kvp.Value;
                }
            }
        }

        return null;
    }

    public static string ResolveRespCenter(string? city)
    {
        if (string.IsNullOrWhiteSpace(city)) return "HO";
        var c = city.Trim().ToUpperInvariant();
        if (c.Contains("MUMBAI") || c.Contains("THANE") || c.Contains("BHIWANDI") || c.Contains("KHARGHAR") || c.Contains("NAVI MUMBAI")) return "MUMBAI";
        if (c.Contains("PUNE") || c.Contains("NASHIK") || c.Contains("AURANGABAD")) return "PUNE";
        if (c.Contains("AHMEDABAD") || c.Contains("SURAT") || c.Contains("VADODARA") || c.Contains("RAJKOT")) return "AHMEDABAD";
        if (c.Contains("BELGAUM") || c.Contains("BELAGAVI") || c.Contains("HUBLI") || c.Contains("DHARWAD")) return "BELGAUM";
        if (c.Contains("HYDERABAD") || c.Contains("SECUNDERABAD")) return "HYDERABAD";
        if (c.Contains("MANGALORE") || c.Contains("UDUPI")) return "MANGALORE";
        if (c.Contains("BANGALORE") || c.Contains("BENGALURU")) return "BANGALORE";
        if (c.Contains("CHENNAI")) return "CHENNAI";
        return "HO";
    }

    public static string? GetCityCoordinates(string? city, string? address = null)
    {
        if (!string.IsNullOrWhiteSpace(city))
        {
            var cleanCity = city.Trim().ToLowerInvariant();
            if (CityToCoordinates.TryGetValue(cleanCity, out var coords))
                return coords;
        }

        if (!string.IsNullOrWhiteSpace(address))
        {
            foreach (var kvp in CityToCoordinates)
            {
                if (Regex.IsMatch(address, $@"\b{Regex.Escape(kvp.Key)}\b", RegexOptions.IgnoreCase))
                {
                    return kvp.Value;
                }
            }
        }

        return "18.5204, 73.8567";
    }

    private static readonly Dictionary<string, string> CityToCoordinates = new(StringComparer.OrdinalIgnoreCase)
    {
        // Maharashtra
        ["pune"] = "18.5204, 73.8567",
        ["mumbai"] = "19.0760, 72.8777",
        ["bhiwandi"] = "19.2967, 73.0631",
        ["thane"] = "19.2183, 72.9781",
        ["navi mumbai"] = "19.0330, 73.0297",
        ["nagpur"] = "21.1458, 79.0882",
        ["nashik"] = "19.9975, 73.7898",
        ["aurangabad"] = "19.8762, 75.3433",
        ["chhatrapati sambhajinagar"] = "19.8762, 75.3433",
        ["solapur"] = "17.6599, 75.9064",
        ["kolhapur"] = "16.7050, 74.2433",
        ["sangli"] = "16.8524, 74.5815",
        ["amravati"] = "20.9374, 77.7796",
        ["nanded"] = "19.1383, 77.3210",
        ["jalgaon"] = "21.0077, 75.5626",
        ["akola"] = "20.7002, 77.0082",
        ["latur"] = "18.4088, 76.5604",
        ["dhule"] = "20.9042, 74.7749",
        ["ahmednagar"] = "19.0948, 74.7480",
        ["chandrapur"] = "19.9615, 79.2961",
        ["parbhani"] = "19.2686, 76.7708",
        ["panvel"] = "18.9894, 73.1175",
        ["satara"] = "17.6805, 74.0183",
        ["palghar"] = "19.6967, 72.7699",
        ["raigad"] = "18.5158, 73.1822",

        // Karnataka
        ["belgaum"] = "15.8497, 74.4977",
        ["belagavi"] = "15.8497, 74.4977",
        ["bangalore"] = "12.9716, 77.5946",
        ["bengaluru"] = "12.9716, 77.5946",
        ["hubli"] = "15.3647, 75.1240",
        ["hubballi"] = "15.3647, 75.1240",
        ["dharwad"] = "15.4589, 75.0078",
        ["mangalore"] = "12.9141, 74.8560",
        ["mangaluru"] = "12.9141, 74.8560",
        ["mysore"] = "12.2958, 76.6394",
        ["mysuru"] = "12.2958, 76.6394",
        ["gulbarga"] = "17.3297, 76.8343",
        ["kalaburagi"] = "17.3297, 76.8343",
        ["bellary"] = "15.1394, 76.9214",
        ["ballari"] = "15.1394, 76.9214",
        ["davangere"] = "14.4644, 75.9218",
        ["shimoga"] = "13.9299, 75.5681",
        ["shivamogga"] = "13.9299, 75.5681",
        ["tumkur"] = "13.3379, 77.1173",
        ["tumakuru"] = "13.3379, 77.1173",
        ["udupi"] = "13.3409, 74.7421",
        ["hassan"] = "13.0033, 76.1004",

        // Gujarat
        ["ahmedabad"] = "23.0225, 72.5714",
        ["surat"] = "21.1702, 72.8311",
        ["vadodara"] = "22.3072, 73.1812",
        ["baroda"] = "22.3072, 73.1812",
        ["rajkot"] = "22.3039, 70.8022",
        ["bhavnagar"] = "21.7645, 72.1519",
        ["jamnagar"] = "22.4707, 70.0577",
        ["gandhinagar"] = "23.2156, 72.6369",
        ["anand"] = "22.5645, 72.9289",
        ["morbi"] = "22.8173, 70.8378",
        ["bharuch"] = "21.7051, 72.9959",
        ["vapi"] = "20.3718, 72.9043",
        ["valsad"] = "20.5992, 72.9342",
        ["ankleshwar"] = "21.6264, 73.0152",

        // Telangana & Andhra Pradesh
        ["hyderabad"] = "17.3850, 78.4867",
        ["secunderabad"] = "17.4399, 78.4983",
        ["warangal"] = "17.9689, 79.5941",
        ["visakhapatnam"] = "17.6868, 83.2185",
        ["vizag"] = "17.6868, 83.2185",
        ["vijayawada"] = "16.5062, 80.6480",
        ["guntur"] = "16.3067, 80.4365",
        ["nellore"] = "14.4426, 79.9865",
        ["tirupati"] = "13.6288, 79.4192",

        // Tamil Nadu
        ["chennai"] = "13.0827, 80.2707",
        ["coimbatore"] = "11.0168, 76.9558",
        ["madurai"] = "9.9252, 78.1198",
        ["trichy"] = "10.7905, 78.7047",
        ["salem"] = "11.6643, 78.1460",
        ["hosur"] = "12.7409, 77.8253",

        // Rajasthan & North
        ["jaipur"] = "26.9124, 75.7873",
        ["jodhpur"] = "26.2389, 73.0243",
        ["udaipur"] = "24.5854, 73.7125",
        ["kota"] = "25.2138, 75.8648",
        ["indore"] = "22.7196, 75.8577",
        ["bhopal"] = "23.2599, 77.4126",
        ["gwalior"] = "26.2183, 78.1828",
        ["lucknow"] = "26.8467, 80.9462",
        ["kanpur"] = "26.4499, 80.3319",
        ["noida"] = "28.5355, 77.3910",
        ["greater noida"] = "28.4744, 77.5040",
        ["delhi"] = "28.6139, 77.2090",
        ["new delhi"] = "28.6139, 77.2090",
        ["gurugram"] = "28.4595, 77.0266",
        ["gurgaon"] = "28.4595, 77.0266",
        ["faridabad"] = "28.4089, 77.3178",
        ["chandigarh"] = "30.7333, 76.7794",
        ["ludhiana"] = "30.9010, 75.8573",
        ["kolkata"] = "22.5726, 88.3639",
        ["kochi"] = "9.9312, 76.2673",
        ["cochin"] = "9.9312, 76.2673",
        ["goa"] = "15.2993, 74.1240",
        ["panaji"] = "15.4909, 73.8278",
        ["patna"] = "25.5941, 85.1376",
        ["ranchi"] = "23.3441, 85.3096",
        ["raipur"] = "21.2514, 81.6296",
        ["guwahati"] = "26.1445, 91.7362",
        ["dehradun"] = "30.3165, 78.0322"
    };

    public async Task<List<DiscoveredLeadDto>> ScrapeLeadsFromWebUrlAsync(
        string url,
        string division,
        string targetProduct,
        int limit,
        CancellationToken ct = default)
    {
        var cleanUrl = (url ?? "").Trim();
        if (string.IsNullOrWhiteSpace(cleanUrl))
            return new List<DiscoveredLeadDto>();

        var cleanDivision = string.IsNullOrWhiteSpace(division) ? "Tyresoles" : division.Trim();
        var cleanProduct = string.IsNullOrWhiteSpace(targetProduct) ? "Commercial Retreading" : targetProduct.Trim();
        var searchLimit = Math.Clamp(limit, 1, 100);

        try
        {
            var leads = await RunPythonHarvesterAsync(
                city: "Commercial Hub",
                product: cleanProduct,
                division: cleanDivision,
                channel: "Web-Scraper",
                maxCount: searchLimit,
                radiusKm: 0,
                ct: ct,
                url: cleanUrl);

            return leads.Take(searchLimit).ToList();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to scrape leads from Web URL {Url}", cleanUrl);
            return new List<DiscoveredLeadDto>();
        }
    }

    private async Task<List<DiscoveredLeadDto>> RunPythonHarvesterAsync(
        string city,
        string product,
        string division,
        string channel,
        int maxCount,
        int radiusKm,
        CancellationToken ct,
        string? url = null)
    {
        var results = new List<DiscoveredLeadDto>();
        var harvesterDir = GetHarvesterDirectory();
        if (string.IsNullOrWhiteSpace(harvesterDir))
        {
            _logger?.LogError("Lead harvester directory could not be located. Looked in AppDomain.CurrentDomain.BaseDirectory ({BaseDir}) and fallback candidates.", AppDomain.CurrentDomain.BaseDirectory);
            return results;
        }

        var pythonScript = Path.Combine(harvesterDir, "main.py");
        if (!File.Exists(pythonScript))
        {
            _logger?.LogError("main.py not found at '{Path}'.", pythonScript);
            return results;
        }

        var pythonExe = GetPythonExecutable();

        var sourceArg = channel switch
        {
            "LinkedIn-OSINT" => "linkedin_osint",
            "OEM-Dealers" => "oem_dealers",
            "Transport-Directory" => "transport_directory",
            "Web-Scraper" or "TransportFamily" => "web_scraper",
            _ => "google_maps"
        };

        var tempJsonFile = Path.Combine(Path.GetTempPath(), $"leads_{Guid.NewGuid():N}.json");

        try
        {
            var urlArg = !string.IsNullOrWhiteSpace(url) ? $"--url \"{url}\"" : "";
            var psi = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = $"\"{pythonScript}\" --division \"{division}\" --product \"{product}\" --city \"{city}\" --radius {radiusKm} --source {sourceArg} --limit {maxCount} {urlArg} --export-json \"{tempJsonFile}\" --dry-run",
                WorkingDirectory = harvesterDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = new Process { StartInfo = psi };
            try
            {
                proc.Start();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to start Python process using executable '{PythonExe}'. Ensure Python is installed and accessible.", pythonExe);
                return results;
            }

            // Read stdout and stderr concurrently to prevent pipe buffer deadlock
            var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = proc.StandardError.ReadToEndAsync(ct);

            // Timeout after 90 seconds to allow deep queries ample time while preventing permanently hung requests
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            try
            {
                await proc.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (!proc.HasExited)
                {
                    try { proc.Kill(entireProcessTree: true); } catch { }
                }
                _logger?.LogWarning("Python harvester timed out after 90s for {Channel} in {City}", channel, city);
            }

            // Await drained streams
            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);

            if (File.Exists(tempJsonFile))
            {
                var json = await File.ReadAllTextAsync(tempJsonFile, ct);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    using var doc = JsonDocument.Parse(json);
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        results.Add(new DiscoveredLeadDto
                        {
                            FullName = item.TryGetProperty("fullName", out var fn) ? fn.GetString() ?? "" : "",
                            CompanyName = item.TryGetProperty("companyName", out var cn) ? cn.GetString() ?? "" : "",
                            MobileNo = item.TryGetProperty("mobileNo", out var mn) ? mn.GetString() ?? "" : "",
                            Address = item.TryGetProperty("address", out var addr) ? addr.GetString() : null,
                            City = item.TryGetProperty("city", out var ctProp) ? ctProp.GetString() ?? city : city,
                            State = item.TryGetProperty("state", out var st) && !string.IsNullOrWhiteSpace(st.GetString()) && !st.GetString()!.Equals("India", StringComparison.OrdinalIgnoreCase)
                                ? st.GetString()
                                : ResolveState(item.TryGetProperty("city", out var cp) ? cp.GetString() ?? city : city, item.TryGetProperty("address", out var ap) ? ap.GetString() : null),
                            Division = division,
                            TargetProduct = product,
                            LeadSourceType = "Automated",
                            LeadSourceChannel = item.TryGetProperty("leadSourceChannel", out var lsc) ? lsc.GetString() ?? channel : channel,
                            SourceUrl = item.TryGetProperty("sourceUrl", out var su) ? su.GetString() ?? "" : "",
                            QualityScore = item.TryGetProperty("qualityScore", out var qs) ? qs.GetDecimal() : 0.85m,
                            ScrapingQuery = item.TryGetProperty("scrapingQuery", out var sq) ? sq.GetString() ?? "" : "",
                            Tags = item.TryGetProperty("tags", out var tg) ? tg.GetString() ?? "" : "",
                            Website = item.TryGetProperty("website", out var ws) ? ws.GetString() : null,
                            Snippet = item.TryGetProperty("notes", out var nt) ? nt.GetString() : null,
                            ContactType = "Lead",
                            Location = item.TryGetProperty("location", out var locP) && !string.IsNullOrWhiteSpace(locP.GetString())
                                ? locP.GetString()
                                : GetCityCoordinates(item.TryGetProperty("city", out var lcp) ? lcp.GetString() ?? city : city, item.TryGetProperty("address", out var lap) ? lap.GetString() : null)
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Python Harvester execution error for {Channel} / {City}", channel, city);
        }
        finally
        {
            if (File.Exists(tempJsonFile))
            {
                try { File.Delete(tempJsonFile); } catch { }
            }
        }

        return results;
    }

    public async Task<CrawlCheckpointDto> GetCrawlCheckpointAsync(string url, CancellationToken ct = default)
    {
        var cleanUrl = (url ?? "").Trim();
        var json = await RunPythonPipelineJsonAsync($"status --url \"{cleanUrl}\"", ct);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new CrawlCheckpointDto { TargetUrl = cleanUrl };
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return ParseCheckpointElement(doc.RootElement, cleanUrl);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to parse crawl checkpoint for {Url}", cleanUrl);
            return new CrawlCheckpointDto { TargetUrl = cleanUrl };
        }
    }

    public async Task<CrawlPipelineResultDto> FetchWebUrlBatchAsync(
        string url,
        int pages,
        bool reset,
        string division,
        string targetProduct,
        CancellationToken ct = default)
    {
        var cleanUrl = (url ?? "").Trim();
        var p = Math.Clamp(pages, 1, 10);
        var resetArg = reset ? "--reset" : "";
        var div = string.IsNullOrWhiteSpace(division) ? "Tyresoles" : division.Trim();
        var prod = string.IsNullOrWhiteSpace(targetProduct) ? "Commercial Retreading" : targetProduct.Trim();

        var json = await RunPythonPipelineJsonAsync($"fetch --url \"{cleanUrl}\" --pages {p} {resetArg} --division \"{div}\" --product \"{prod}\"", ct);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new CrawlPipelineResultDto { Success = false, Message = "Pipeline fetch returned empty response." };
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var res = new CrawlPipelineResultDto
            {
                Success = root.TryGetProperty("success", out var s) && s.GetBoolean(),
                PagesCrawled = root.TryGetProperty("pages_crawled", out var pc) ? pc.GetInt32() : 0,
                ListingsDiscovered = root.TryGetProperty("listings_discovered", out var ld) ? ld.GetInt32() : 0,
                NewLeadsEnqueued = root.TryGetProperty("new_leads_enqueued", out var nle) ? nle.GetInt32() : 0
            };
            res.Checkpoint = await GetCrawlCheckpointAsync(cleanUrl, ct);
            res.Message = $"Fetched {res.PagesCrawled} page(s), enqueued {res.NewLeadsEnqueued} leads into staging buffer.";
            return res;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed parsing fetch result for {Url}", cleanUrl);
            return new CrawlPipelineResultDto { Success = false, Message = ex.Message };
        }
    }

    public async Task<CrawlPipelineResultDto> ProcessStagedLeadsAsync(
        string? url,
        int limit,
        bool dryRun,
        string? defaultLeadSourceType = null,
        string? defaultLeadSourceChannel = null,
        string? defaultRespCenter = null,
        CancellationToken ct = default)
    {
        var cleanUrl = !string.IsNullOrWhiteSpace(url) ? url.Trim() : null;
        var urlArg = !string.IsNullOrWhiteSpace(cleanUrl) ? $"--url \"{cleanUrl}\"" : "";
        var lim = Math.Clamp(limit, 1, 500);

        // 1. Fetch pending leads from Python staging buffer
        var json = await RunPythonPipelineJsonAsync($"pending {urlArg} --limit {lim}", ct);
        if (string.IsNullOrWhiteSpace(json))
        {
            var emptyCp = !string.IsNullOrWhiteSpace(cleanUrl) ? await GetCrawlCheckpointAsync(cleanUrl, ct) : null;
            return new CrawlPipelineResultDto
            {
                Success = true,
                Message = "No pending leads in staging buffer.",
                Checkpoint = emptyCp
            };
        }

        var leads = new List<JsonElement>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    leads.Add(item.Clone());
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to parse pending leads from Python");
            return new CrawlPipelineResultDto { Success = false, Message = ex.Message };
        }

        if (leads.Count == 0)
        {
            var emptyCp = !string.IsNullOrWhiteSpace(cleanUrl) ? await GetCrawlCheckpointAsync(cleanUrl, ct) : null;
            return new CrawlPipelineResultDto
            {
                Success = true,
                Message = "No pending staged leads found.",
                Checkpoint = emptyCp
            };
        }

        // 2. Fetch existing CRM contact mobile numbers for deduplication
        var existingMobiles = await _crmDb.CrmContacts
            .Where(c => c.MobileNo != null && c.MobileNo != "")
            .Select(c => c.MobileNo!)
            .ToListAsync(ct);

        var existingMobileSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var m in existingMobiles)
        {
            var d = Regex.Replace(m, @"[^\d]", "");
            if (d.Length >= 10) existingMobileSet.Add(d.Substring(d.Length - 10));
        }

        var importedMap = new Dictionary<string, string>(); // staging_id -> crmContactId
        var duplicateIds = new List<string>();
        var rejectedIds = new List<string>();
        var newContacts = new List<CrmContact>();
        var seenBatchMobiles = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in leads)
        {
            var sid = item.TryGetProperty("staging_id", out var sidP) ? sidP.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(sid)) continue;

            var ld = item.TryGetProperty("lead_data", out var ldP) ? ldP : default;
            var comp = ld.TryGetProperty("companyName", out var cn) ? cn.GetString()?.Trim() : null;
            var contact = ld.TryGetProperty("fullName", out var fn) ? fn.GetString()?.Trim() : null;
            var rawPhone = ld.TryGetProperty("mobileNo", out var ph) ? ph.GetString() ?? "" : "";
            var phone2 = ld.TryGetProperty("mobileNo2", out var ph2) ? ph2.GetString() : null;
            var address = ld.TryGetProperty("address", out var ad) ? ad.GetString() : null;
            var city = ld.TryGetProperty("city", out var ci) ? ci.GetString() : null;
            var state = ld.TryGetProperty("state", out var st) ? st.GetString() : null;
            var location = ld.TryGetProperty("location", out var loc) ? loc.GetString() : null;
            var website = ld.TryGetProperty("website", out var wb) ? wb.GetString() : null;
            var sourceUrl = item.TryGetProperty("source_url", out var su) ? su.GetString() : null;
            var division = ld.TryGetProperty("division", out var dv) ? dv.GetString() ?? "Tyresoles" : "Tyresoles";
            var product = ld.TryGetProperty("targetProduct", out var tp) ? tp.GetString() ?? "Commercial Retreading" : "Commercial Retreading";

            var digits = Regex.Replace(rawPhone, @"[^\d]", "");
            string normalizedMobile = digits.Length >= 10 ? digits.Substring(digits.Length - 10) : "";

            // Validate phone: 10 digits and not 6+ repeating digits
            if (normalizedMobile.Length != 10 || Regex.IsMatch(normalizedMobile, @"^(\d)\1{5,}$"))
            {
                rejectedIds.Add(sid);
                continue;
            }

            // Deduplicate against CRM database and current batch
            if (existingMobileSet.Contains(normalizedMobile) || seenBatchMobiles.Contains(normalizedMobile))
            {
                duplicateIds.Add(sid);
                continue;
            }

            seenBatchMobiles.Add(normalizedMobile);
            existingMobileSet.Add(normalizedMobile);

            var contactName = !string.IsNullOrWhiteSpace(contact) ? contact : (!string.IsNullOrWhiteSpace(comp) ? comp : "Commercial Lead");
            var resolvedState = !string.IsNullOrWhiteSpace(state) && !state.Equals("India", StringComparison.OrdinalIgnoreCase)
                ? state
                : ResolveState(city, address);

            var finalRespCenter = !string.IsNullOrWhiteSpace(defaultRespCenter)
                ? defaultRespCenter.Trim()
                : ResolveRespCenter(city);
            var finalLeadSourceType = !string.IsNullOrWhiteSpace(defaultLeadSourceType)
                ? defaultLeadSourceType.Trim()
                : "Automated";
            var finalLeadSourceChannel = !string.IsNullOrWhiteSpace(defaultLeadSourceChannel)
                ? defaultLeadSourceChannel.Trim()
                : "Web-Harvester";

            var newContact = new CrmContact
            {
                Id = Guid.NewGuid(),
                FullName = contactName,
                CompanyName = comp,
                MobileNo = normalizedMobile,
                MobileNo2 = phone2,
                Address = address,
                City = city,
                State = resolvedState,
                RespCenter = finalRespCenter,
                Division = division,
                TargetProduct = product,
                LeadSourceType = finalLeadSourceType,
                LeadSourceChannel = finalLeadSourceChannel,
                SourceUrl = sourceUrl,
                Website = website,
                Location = location,
                ContactType = "Lead",
                Tags = $"{finalLeadSourceType}, {finalLeadSourceChannel}, {division}, {product}, {city}",
                IsActive = true,
                CreatedBy = "system-harvester",
                CreatedAt = DateTime.UtcNow,
                HarvestedAt = DateTime.UtcNow
            };

            newContacts.Add(newContact);
            importedMap[sid] = newContact.Id.ToString();
        }

        // 3. Save genuine leads to SQL Server CRM
        if (!dryRun && newContacts.Count > 0)
        {
            _crmDb.CrmContacts.AddRange(newContacts);
            await _crmDb.SaveChangesAsync(ct);
            _logger?.LogInformation("Successfully inserted {Count} new leads into CrmContacts.", newContacts.Count);
        }

        // 4. Update status in SQLite staging buffer
        if (importedMap.Count > 0 || duplicateIds.Count > 0 || rejectedIds.Count > 0)
        {
            var markPayload = JsonSerializer.Serialize(new
            {
                imported = importedMap,
                duplicates = duplicateIds,
                rejected = rejectedIds
            });

            var tempFile = Path.Combine(Path.GetTempPath(), $"mark_{Guid.NewGuid():N}.json");
            try
            {
                await File.WriteAllTextAsync(tempFile, markPayload, ct);
                await RunPythonPipelineJsonAsync($"mark_imported --file \"{tempFile}\" {urlArg}", ct);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }

        var checkpoint = !string.IsNullOrWhiteSpace(cleanUrl) ? await GetCrawlCheckpointAsync(cleanUrl, ct) : null;
        return new CrawlPipelineResultDto
        {
            Success = true,
            ProcessedCount = leads.Count,
            ImportedCount = newContacts.Count,
            DuplicateCount = duplicateIds.Count,
            RejectedCount = rejectedIds.Count,
            PendingRemaining = checkpoint?.QueueSummary?.Pending ?? 0,
            Checkpoint = checkpoint,
            Message = $"Processed {leads.Count} staged leads: {newContacts.Count} imported into CRM Contacts, {duplicateIds.Count} duplicates skipped, {rejectedIds.Count} rejected."
        };
    }

    public async Task<AutoExtractResultDto> AutoExtractWebLeadsAsync(
        string url,
        int pages,
        bool autoIngest,
        bool reset,
        string division,
        string targetProduct,
        bool dryRun,
        string? defaultLeadSourceType = null,
        string? defaultLeadSourceChannel = null,
        string? defaultRespCenter = null,
        CancellationToken ct = default)
    {
        var cleanUrl = (url ?? "").Trim();
        var p = Math.Clamp(pages, 1, 10);
        var resetArg = reset ? "--reset" : "";
        var div = string.IsNullOrWhiteSpace(division) ? "Tyresoles" : division.Trim();
        var prod = string.IsNullOrWhiteSpace(targetProduct) ? "Commercial Retreading" : targetProduct.Trim();

        // Run Process 1: Page scouting, badge detection, parallel worker extraction into staging.db
        var json = await RunPythonPipelineJsonAsync($"auto_extract --url \"{cleanUrl}\" --pages {p} --no-ingest {resetArg} --division \"{div}\" --product \"{prod}\"", ct);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new AutoExtractResultDto { Success = false, Message = "Auto-extract worker returned empty response.", TargetUrl = cleanUrl };
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var res = new AutoExtractResultDto
            {
                Success = root.TryGetProperty("success", out var s) && s.GetBoolean(),
                TargetUrl = cleanUrl
            };

            if (root.TryGetProperty("stage1_fetch", out var f) && f.ValueKind == JsonValueKind.Object)
            {
                res.PagesCrawled = f.TryGetProperty("pages_crawled", out var pc) ? pc.GetInt32() : 0;
                res.ListingsDiscovered = f.TryGetProperty("listings_discovered", out var ld) ? ld.GetInt32() : 0;
                res.NewLeadsEnqueued = f.TryGetProperty("new_leads_enqueued", out var nle) ? nle.GetInt32() : 0;
            }

            if (root.TryGetProperty("badge_metadata", out var bm) && bm.ValueKind == JsonValueKind.Object)
            {
                res.BadgeMetadata = new BadgeMetadataDto
                {
                    HasBadgeDetected = bm.TryGetProperty("has_badge_detected", out var hbd) && hbd.GetBoolean(),
                    BadgeText = bm.TryGetProperty("badge_text", out var bt) ? bt.GetString() : null,
                    CurrentBadgePage = bm.TryGetProperty("current_badge_page", out var cbp) && cbp.ValueKind == JsonValueKind.Number ? cbp.GetInt32() : null,
                    TotalBadgePages = bm.TryGetProperty("total_badge_pages", out var tbp) && tbp.ValueKind == JsonValueKind.Number ? tbp.GetInt32() : null,
                    NextBadgeUrl = bm.TryGetProperty("next_badge_url", out var nbu) ? nbu.GetString() : null,
                    TotalItemsCount = bm.TryGetProperty("total_items_count", out var tic) && tic.ValueKind == JsonValueKind.Number ? tic.GetInt32() : null
                };
            }

            // Ingest to CRM directly in .NET Core if autoIngest is requested
            if (autoIngest)
            {
                var ingestRes = await ProcessStagedLeadsAsync(cleanUrl, 100, dryRun, defaultLeadSourceType, defaultLeadSourceChannel, defaultRespCenter, ct);
                res.ProcessedCount = ingestRes.ProcessedCount;
                res.ImportedCount = ingestRes.ImportedCount;
                res.DuplicateCount = ingestRes.DuplicateCount;
                res.RejectedCount = ingestRes.RejectedCount;
                res.Checkpoint = ingestRes.Checkpoint;
            }
            else
            {
                res.Checkpoint = await GetCrawlCheckpointAsync(cleanUrl, ct);
            }

            res.Leads = await GetStagedLeadsAsync(cleanUrl, null, 50, ct);
            res.Message = $"Auto-extracted {res.PagesCrawled} page(s) ({res.ListingsDiscovered} listings discovered, {res.NewLeadsEnqueued} enqueued). Ingested: {res.ImportedCount} new, {res.DuplicateCount} duplicates.";
            return res;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed parsing auto_extract result for {Url}", cleanUrl);
            return new AutoExtractResultDto { Success = false, Message = ex.Message, TargetUrl = cleanUrl };
        }
    }

    public async Task<List<StagedLeadItemDto>> GetStagedLeadsAsync(
        string? url,
        string? status,
        int limit,
        CancellationToken ct = default)
    {
        var urlArg = !string.IsNullOrWhiteSpace(url) ? $"--url \"{url.Trim()}\"" : "";
        var statusArg = !string.IsNullOrWhiteSpace(status) ? $"--status \"{status.Trim()}\"" : "";
        var lim = Math.Clamp(limit, 1, 200);

        var json = await RunPythonPipelineJsonAsync($"leads {urlArg} {statusArg} --limit {lim}", ct);
        var list = new List<StagedLeadItemDto>();
        if (string.IsNullOrWhiteSpace(json)) return list;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    list.Add(ParseStagedLeadItem(item));
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to parse staged leads json");
        }

        return list;
    }

    public async Task<CrawlCheckpointDto> ResetCrawlCheckpointAsync(
        string url,
        bool clearStaging,
        CancellationToken ct = default)
    {
        var cleanUrl = (url ?? "").Trim();
        var clearArg = clearStaging ? "--clear-staging" : "";
        await RunPythonPipelineJsonAsync($"reset --url \"{cleanUrl}\" {clearArg}", ct);
        return await GetCrawlCheckpointAsync(cleanUrl, ct);
    }

    private async Task<string> RunPythonPipelineJsonAsync(string args, CancellationToken ct)
    {
        var harvesterDir = GetHarvesterDirectory();
        if (string.IsNullOrWhiteSpace(harvesterDir))
        {
            _logger?.LogError("Lead harvester directory could not be located. Looked in AppDomain.CurrentDomain.BaseDirectory ({BaseDir}) and fallback candidates. Ensure harvester is deployed.", AppDomain.CurrentDomain.BaseDirectory);
            return string.Empty;
        }

        var pythonScript = Path.Combine(harvesterDir, "crawl_pipeline.py");
        if (!File.Exists(pythonScript))
        {
            _logger?.LogError("crawl_pipeline.py not found at '{Path}'.", pythonScript);
            return string.Empty;
        }

        var pythonExe = GetPythonExecutable();

        var psi = new ProcessStartInfo
        {
            FileName = pythonExe,
            Arguments = $"\"{pythonScript}\" {args}",
            WorkingDirectory = harvesterDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = new Process { StartInfo = psi };
        try
        {
            proc.Start();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start Python process using executable '{PythonExe}'. Ensure Python is installed and added to PATH on the server.", pythonExe);
            return string.Empty;
        }

        var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = proc.StandardError.ReadToEndAsync(ct);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            await proc.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!proc.HasExited)
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
            }
            _logger?.LogWarning("Python pipeline execution timed out for {Args}", args);
        }

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            _logger?.LogWarning("Python pipeline stderr: {Stderr}", stderr);
        }

        // Return only the JSON block from stdout (extract between { and } or [ and ] if log lines present)
        var firstBracket = stdout.IndexOf('[');
        var lastBracket = stdout.LastIndexOf(']');
        var firstBrace = stdout.IndexOf('{');
        var lastBrace = stdout.LastIndexOf('}');

        if (firstBracket >= 0 && (firstBrace < 0 || firstBracket < firstBrace) && lastBracket > firstBracket)
        {
            return stdout.Substring(firstBracket, lastBracket - firstBracket + 1);
        }
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            return stdout.Substring(firstBrace, lastBrace - firstBrace + 1);
        }

        return stdout.Trim();
    }

    private static StagedLeadItemDto ParseStagedLeadItem(JsonElement item)
    {
        return new StagedLeadItemDto
        {
            Id = item.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
            TargetUrl = item.TryGetProperty("targetUrl", out var tu) ? tu.GetString() ?? "" : "",
            SourceUrl = item.TryGetProperty("sourceUrl", out var su) ? su.GetString() ?? "" : "",
            PageNum = item.TryGetProperty("pageNum", out var pn) ? pn.GetInt32() : 1,
            CompanyName = item.TryGetProperty("companyName", out var cn) ? cn.GetString() : null,
            ContactPerson = item.TryGetProperty("contactPerson", out var cp) ? cp.GetString() : null,
            MobileNo = item.TryGetProperty("mobileNo", out var mn) ? mn.GetString() : null,
            AltMobileNo = item.TryGetProperty("altMobileNo", out var amn) ? amn.GetString() : null,
            Email = item.TryGetProperty("email", out var em) ? em.GetString() : null,
            City = item.TryGetProperty("city", out var ct) ? ct.GetString() : null,
            State = item.TryGetProperty("state", out var st) ? st.GetString() : null,
            Location = item.TryGetProperty("location", out var loc) ? loc.GetString() : null,
            Website = item.TryGetProperty("website", out var wb) ? wb.GetString() : null,
            Status = item.TryGetProperty("status", out var s) ? s.GetString() ?? "PENDING" : "PENDING",
            ValidationError = item.TryGetProperty("validationError", out var ve) ? ve.GetString() : null,
            CrmContactId = item.TryGetProperty("crmContactId", out var cci) ? cci.GetString() : null,
            CreatedAt = item.TryGetProperty("createdAt", out var ca) ? ca.GetString() : null,
            ProcessedAt = item.TryGetProperty("processedAt", out var pa) ? pa.GetString() : null
        };
    }

    private static CrawlCheckpointDto ParseCheckpointElement(JsonElement root, string cleanUrl)
    {
        var cp = new CrawlCheckpointDto
        {
            TargetUrl = root.TryGetProperty("target_url", out var tu) ? tu.GetString() ?? cleanUrl : cleanUrl,
            LastCrawledPage = root.TryGetProperty("last_crawled_page", out var lp) ? lp.GetInt32() : 0,
            NextPageToCrawl = root.TryGetProperty("next_page_to_crawl", out var np) ? np.GetInt32() : 1,
            TotalPagesCrawled = root.TryGetProperty("total_pages_crawled", out var tpc) ? tpc.GetInt32() : 0,
            TotalListingsDiscovered = root.TryGetProperty("total_listings_discovered", out var tld) ? tld.GetInt32() : 0,
            TotalPagesDetected = root.TryGetProperty("total_pages_detected", out var tpd) ? tpd.GetInt32() : 0,
            HasReachedEnd = root.TryGetProperty("has_reached_end", out var hre) && hre.GetBoolean(),
            UpdatedAt = root.TryGetProperty("updated_at", out var ua) ? ua.GetString() : null
        };

        if (root.TryGetProperty("queue_summary", out var qs))
        {
            cp.QueueSummary = new CrawlQueueSummaryDto
            {
                Total = qs.TryGetProperty("total", out var tot) ? tot.GetInt32() : 0,
                Pending = qs.TryGetProperty("pending", out var pen) ? pen.GetInt32() : 0,
                Imported = qs.TryGetProperty("imported", out var imp) ? imp.GetInt32() : 0,
                Duplicate = qs.TryGetProperty("duplicate", out var dup) ? dup.GetInt32() : 0,
                Rejected = qs.TryGetProperty("rejected", out var rej) ? rej.GetInt32() : 0
            };
        }

        return cp;
    }

    private string GetHarvesterDirectory()
    {
        var configuredDir = _configuration["LeadHarvester:HarvesterDirectory"];
        if (!string.IsNullOrWhiteSpace(configuredDir))
        {
            if (Path.IsPathRooted(configuredDir) && Directory.Exists(configuredDir))
                return configuredDir;

            var relApp = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configuredDir));
            if (Directory.Exists(relApp))
                return relApp;
        }

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "harvester"),
            Path.GetFullPath(Path.Combine(baseDir, "..", "harvester")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "harvester")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "harvester")),
            @"d:\Work Desk\Tyresoles\harvester"
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate) && (File.Exists(Path.Combine(candidate, "crawl_pipeline.py")) || File.Exists(Path.Combine(candidate, "main.py"))))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    private string GetPythonExecutable()
    {
        return _configuration["LeadHarvester:PythonExecutable"] ?? "python";
    }
}
