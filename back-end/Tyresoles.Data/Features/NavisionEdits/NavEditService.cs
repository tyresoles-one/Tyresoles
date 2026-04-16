using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Tyresoles.Data.Features.Calendar;
using Tyresoles.Data.Features.Calendar.Entities;
using Tyresoles.Data.Features.Common;
using Tyresoles.Data.Features.NavisionEdits.Entities;

namespace Tyresoles.Data.Features.NavisionEdits;

public sealed class NavEditService : INavEditService
{
    private readonly NavEditDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly INotificationService _notificationService;
    private readonly Connector _connector;
    private readonly ILogger<NavEditService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public NavEditService(
        NavEditDbContext db,
        IConfiguration configuration,
        INotificationService notificationService,
        Connector connector,
        ILogger<NavEditService> logger)
    {
        _db = db;
        _configuration = configuration;
        _notificationService = notificationService;
        _connector = connector;
        _logger = logger;
    }

    // ── Request Types (Admin CRUD) ──────────────────────────────

    public async Task<IReadOnlyList<NavEditRequestType>> GetRequestTypesAsync(bool activeOnly = true, CancellationToken ct = default)
    {
        var query = _db.RequestTypes.AsQueryable();
        if (activeOnly)
            query = query.Where(t => t.IsActive);
        return await query.OrderBy(t => t.SortOrder).ThenBy(t => t.Name).ToListAsync(ct);
    }

    public async Task<NavEditRequestType?> GetRequestTypeByIdAsync(int id, CancellationToken ct = default)
    {
        return await _db.RequestTypes.FindAsync([id], ct);
    }

    public async Task<NavEditRequestType> SaveRequestTypeAsync(NavEditRequestTypeInput input, string userId, CancellationToken ct = default)
    {
        NavEditRequestType entity;
        if (input.Id.HasValue && input.Id.Value > 0)
        {
            entity = await _db.RequestTypes.FindAsync([input.Id.Value], ct)
                ?? throw new InvalidOperationException($"Request type {input.Id.Value} not found.");
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = userId;
        }
        else
        {
            entity = new NavEditRequestType
            {
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            };
            _db.RequestTypes.Add(entity);
        }

        entity.Name = input.Name;
        entity.Code = input.Code.ToUpperInvariant();
        entity.Description = input.Description;
        entity.Icon = input.Icon;
        entity.NavTable = input.NavTable;
        entity.NavPrimaryKeyColumn = input.NavPrimaryKeyColumn;
        entity.FieldsJson = input.FieldsJson;
        entity.IsActive = input.IsActive;
        entity.SortOrder = input.SortOrder;

        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<bool> DeleteRequestTypeAsync(int id, CancellationToken ct = default)
    {
        var entity = await _db.RequestTypes.FindAsync([id], ct);
        if (entity == null) return false;
        entity.IsActive = false;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ── Record Lookup (dynamic from Db_Live) ────────────────────

    private string GetNavLiveConnectionString()
    {
        return _configuration["Tyresoles:Tenants:NavLive:ConnectionString"]
            ?? throw new InvalidOperationException("NavLive connection string not configured.");
    }

    public async Task<List<Dictionary<string, object?>>> LookupRecordsAsync(int requestTypeId, string? search, int take = 20, CancellationToken ct = default)
    {
        var reqType = await _db.RequestTypes.FindAsync([requestTypeId], ct)
            ?? throw new InvalidOperationException($"Request type {requestTypeId} not found.");

        var template = ParseTemplate(reqType.FieldsJson);
        // Must not treat "array with only empty column specs" as configured — that yields SELECT PK only.
        var displayColumns = ResolveDisplayColumnsForLookup(template, reqType.FieldsJson, reqType.NavPrimaryKeyColumn);

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { reqType.NavPrimaryKeyColumn };
        foreach (var spec in displayColumns)
        {
            var col = spec.Column?.Trim();
            if (!string.IsNullOrEmpty(col)) columns.Add(col);
        }

        if (template.SearchColumns != null)
        {
            foreach (var sc in template.SearchColumns)
            {
                var col = sc?.Trim();
                if (!string.IsNullOrEmpty(col)) columns.Add(col);
            }
        }

        if (template.LookupFilters != null)
        {
            foreach (var lf in template.LookupFilters)
            {
                var col = lf.Column?.Trim();
                if (!string.IsNullOrEmpty(col)) columns.Add(col);
            }
        }

        var selectClause = string.Join(", ", columns.Select(c => $"[{SanitizeColumnName(c)}]"));
        var tableName = SanitizeTableName(reqType.NavTable);
        var pkCol = SanitizeColumnName(reqType.NavPrimaryKeyColumn);

        var sql = $"SELECT TOP (@take) {selectClause} FROM [{tableName}]";
        var parameters = new List<SqlParameter> { new("@take", take) };

        var whereParts = new List<string>();
        var lfParam = 0;

        if (!string.IsNullOrWhiteSpace(search))
        {
            const string navLikePredicate =
                "CAST([{0}] AS NVARCHAR(4000)) COLLATE Latin1_General_CI_AS LIKE @search COLLATE Latin1_General_CI_AS";
            var searchCols = template.SearchColumns?
                .Select(s => s?.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(SanitizeColumnName)
                .ToList() ?? new List<string>();

            if (searchCols.Count == 0)
                whereParts.Add(string.Format(navLikePredicate, pkCol));
            else
                whereParts.Add("(" + string.Join(" OR ", searchCols.Select(c => string.Format(navLikePredicate, c))) + ")");

            var escapedSearch = EscapeSqlServerLikeWildcard(search);
            parameters.Add(new SqlParameter("@search", $"%{escapedSearch}%"));
        }

        AppendLookupFilterPredicates(whereParts, parameters, template, ref lfParam);

        if (whereParts.Count > 0)
            sql += " WHERE " + string.Join(" AND ", whereParts);

        sql += $" ORDER BY [{pkCol}]";

        return await ExecuteNavQuery(sql, parameters, ct);
    }

    /// <summary>SQL Server LIKE: treat % and _ in user input as literals.</summary>
    private static string EscapeSqlServerLikeWildcard(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("[", "[[]", StringComparison.Ordinal).Replace("%", "[%]", StringComparison.Ordinal).Replace("_", "[_]", StringComparison.Ordinal);
    }

    private static void AppendLookupFilterPredicates(
        List<string> whereParts,
        List<SqlParameter> parameters,
        FieldsTemplate template,
        ref int paramIndex)
    {
        if (template.LookupFilters == null) return;

        foreach (var f in template.LookupFilters)
        {
            var colRaw = f.Column?.Trim();
            if (string.IsNullOrEmpty(colRaw)) continue;
            var col = SanitizeColumnName(colRaw);
            var op = (f.Op ?? "eq").Trim().ToLowerInvariant();
            var castExpr = $"CAST([{col}] AS NVARCHAR(4000)) COLLATE Latin1_General_CI_AS";

            switch (op)
            {
                case "eq":
                {
                    var pn = $"@lf{paramIndex++}";
                    whereParts.Add($"{castExpr} = {pn} COLLATE Latin1_General_CI_AS");
                    parameters.Add(new SqlParameter(pn, f.Value ?? ""));
                    break;
                }
                case "neq":
                {
                    var pn = $"@lf{paramIndex++}";
                    whereParts.Add($"{castExpr} <> {pn} COLLATE Latin1_General_CI_AS");
                    parameters.Add(new SqlParameter(pn, f.Value ?? ""));
                    break;
                }
                case "contains":
                {
                    var pn = $"@lf{paramIndex++}";
                    whereParts.Add($"{castExpr} LIKE {pn} COLLATE Latin1_General_CI_AS");
                    var pat = "%" + EscapeSqlServerLikeWildcard(f.Value ?? "") + "%";
                    parameters.Add(new SqlParameter(pn, pat));
                    break;
                }
                case "notcontains":
                {
                    var pn = $"@lf{paramIndex++}";
                    whereParts.Add($"NOT ({castExpr} LIKE {pn} COLLATE Latin1_General_CI_AS)");
                    var pat = "%" + EscapeSqlServerLikeWildcard(f.Value ?? "") + "%";
                    parameters.Add(new SqlParameter(pn, pat));
                    break;
                }
                case "starts":
                {
                    var pn = $"@lf{paramIndex++}";
                    whereParts.Add($"{castExpr} LIKE {pn} COLLATE Latin1_General_CI_AS");
                    parameters.Add(new SqlParameter(pn, EscapeSqlServerLikeWildcard(f.Value ?? "") + "%"));
                    break;
                }
                case "ends":
                {
                    var pn = $"@lf{paramIndex++}";
                    whereParts.Add($"{castExpr} LIKE {pn} COLLATE Latin1_General_CI_AS");
                    parameters.Add(new SqlParameter(pn, "%" + EscapeSqlServerLikeWildcard(f.Value ?? "")));
                    break;
                }
                case "in":
                {
                    var vals = (f.Values ?? new List<string>())
                        .Select(v => v?.Trim())
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToList();
                    if (vals.Count == 0) continue;
                    var names = new List<string>();
                    foreach (var v in vals)
                    {
                        var pn = $"@lf{paramIndex++}";
                        names.Add(pn);
                        parameters.Add(new SqlParameter(pn, v));
                    }
                    whereParts.Add($"{castExpr} IN ({string.Join(", ", names)})");
                    break;
                }
                case "nin":
                {
                    var vals = (f.Values ?? new List<string>())
                        .Select(v => v?.Trim())
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToList();
                    if (vals.Count == 0) continue;
                    var names = new List<string>();
                    foreach (var v in vals)
                    {
                        var pn = $"@lf{paramIndex++}";
                        names.Add(pn);
                        parameters.Add(new SqlParameter(pn, v));
                    }
                    whereParts.Add($"{castExpr} NOT IN ({string.Join(", ", names)})");
                    break;
                }
                case "isnull":
                    whereParts.Add($"[{col}] IS NULL");
                    break;
                case "isnotnull":
                    whereParts.Add($"[{col}] IS NOT NULL");
                    break;
                default:
                    continue;
            }
        }
    }

    public async Task<Dictionary<string, object?>?> GetRecordByKeyAsync(int requestTypeId, string recordKey, CancellationToken ct = default)
    {
        var reqType = await _db.RequestTypes.FindAsync([requestTypeId], ct)
            ?? throw new InvalidOperationException($"Request type {requestTypeId} not found.");

        var template = ParseTemplate(reqType.FieldsJson);
        var allColumns = CollectColumnsForRecordFetch(reqType, template);

        var selectClause = string.Join(", ", allColumns.Select(c => $"[{SanitizeColumnName(c)}]"));
        var tableName = SanitizeTableName(reqType.NavTable);
        var pkCol = SanitizeColumnName(reqType.NavPrimaryKeyColumn);

        var sql = $"SELECT TOP 1 {selectClause} FROM [{tableName}] WHERE [{pkCol}] = @key";
        var parameters = new List<SqlParameter> { new("@key", recordKey) };

        var results = await ExecuteNavQuery(sql, parameters, ct);
        return results.Count > 0 ? results[0] : null;
    }

    private static HashSet<string> CollectColumnsForRecordFetch(NavEditRequestType reqType, FieldsTemplate template)
    {
        var allColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { reqType.NavPrimaryKeyColumn };

        if (template.DisplayColumns != null)
            foreach (var spec in template.DisplayColumns)
            {
                var col = spec.Column?.Trim();
                if (!string.IsNullOrEmpty(col)) allColumns.Add(col);
            }
        if (template.SearchColumns != null)
            foreach (var c in template.SearchColumns)
            {
                var col = c?.Trim();
                if (!string.IsNullOrEmpty(col)) allColumns.Add(col);
            }
        if (template.LookupFilters != null)
            foreach (var lf in template.LookupFilters)
            {
                var col = lf.Column?.Trim();
                if (!string.IsNullOrEmpty(col)) allColumns.Add(col);
            }
        if (template.Fields != null)
            foreach (var f in template.Fields)
                if (!string.IsNullOrEmpty(f.Column)) allColumns.Add(f.Column);
        if (template.Approvals != null)
        {
            foreach (var a in template.Approvals)
            {
                if (a.When == null) continue;
                foreach (var w in a.When)
                    if (!string.IsNullOrEmpty(w.Column)) allColumns.Add(w.Column);
            }
        }

        return allColumns;
    }

    private async Task<List<Dictionary<string, object?>>> ExecuteNavQuery(string sql, List<SqlParameter> parameters, CancellationToken ct)
    {
        var results = new List<Dictionary<string, object?>>();
        var connStr = GetNavLiveConnectionString();

        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        foreach (var p in parameters) cmd.Parameters.Add(p);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                row[reader.GetName(i)] = value;
            }
            results.Add(row);
        }

        return results;
    }

    /// <summary>Read a single Nav column for the primary key row (used when Req* params are not present in submitted changes).</summary>
    private async Task<string?> GetLiveNavColumnValueAsync(
        NavEditRequestType reqType,
        string recordKey,
        string columnName,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(columnName) || string.IsNullOrWhiteSpace(recordKey))
            return null;
        var col = SanitizeColumnName(columnName.Trim());
        if (string.IsNullOrEmpty(col))
            return null;
        var tableName = SanitizeTableName(reqType.NavTable);
        var pkCol = SanitizeColumnName(reqType.NavPrimaryKeyColumn);
        var sql = $"SELECT TOP 1 [{col}] FROM [{tableName}] WHERE [{pkCol}] = @key";
        var parameters = new List<SqlParameter> { new("@key", recordKey) };
        var results = await ExecuteNavQuery(sql, parameters, ct);
        if (results.Count == 0 || results[0].Count == 0)
            return null;
        return results[0].Values.FirstOrDefault()?.ToString();
    }

    private static string GetConnectorMappedColumnName(FieldsTemplate template, string logicalKey, string fallbackDefault)
    {
        if (template.ConnectorParamColumns != null &&
            template.ConnectorParamColumns.TryGetValue(logicalKey, out var mc) &&
            !string.IsNullOrWhiteSpace(mc))
            return mc.Trim();
        return fallbackDefault;
    }

    public async Task<IReadOnlyList<string>> GetNavLiveTableNamesAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE'
              AND TABLE_NAME NOT LIKE 'timestamp'
            ORDER BY TABLE_NAME
            """;

        return await QueryNavLiveStringsAsync(sql, null, ct);
    }

    public async Task<IReadOnlyList<string>> GetNavLiveColumnNamesForTableAsync(string tableName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            return Array.Empty<string>();

        var safeTable = SanitizeTableName(tableName);
        if (string.IsNullOrEmpty(safeTable))
            return Array.Empty<string>();

        const string sql = """
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = @table
            ORDER BY ORDINAL_POSITION
            """;

        var p = new SqlParameter("@table", safeTable);
        return await QueryNavLiveStringsAsync(sql, new[] { p }, ct);
    }

    private async Task<IReadOnlyList<string>> QueryNavLiveStringsAsync(string sql, SqlParameter[]? parameters, CancellationToken ct)
    {
        var list = new List<string>();
        var connStr = GetNavLiveConnectionString();

        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        if (parameters != null)
            foreach (var p in parameters)
                cmd.Parameters.Add(p);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            if (!reader.IsDBNull(0))
                list.Add(reader.GetString(0));
        }

        return list;
    }

    // ── Requests ─────────────────────────────────────────────────

    public async Task<NavEditRequest> SubmitRequestAsync(NavEditRequestInput input, string userId, string? userFullName, CancellationToken ct = default)
    {
        var reqType = await _db.RequestTypes.FindAsync([input.RequestTypeId], ct)
            ?? throw new InvalidOperationException($"Request type {input.RequestTypeId} not found.");

        var recordKey = input.RecordKey?.Trim() ?? "";
        var hasPendingDuplicate = await _db.Requests.AsNoTracking().AnyAsync(
            r => r.UserId == userId
                 && r.RequestTypeId == input.RequestTypeId
                 && r.RecordKey == recordKey
                 && (r.Status == NavEditStatus.Pending || r.Status == NavEditStatus.PendingApproval),
            ct);
        if (hasPendingDuplicate)
            throw new InvalidOperationException(
                "You already have a pending request for this record. Wait for it to be processed or withdrawn before submitting again.");

        var template = ParseTemplate(reqType.FieldsJson);

        TryParseRequestBodyMode(input.RequestBody, out var reqMode, out var sourceRecordKey);
        if (string.Equals(reqMode, "create", StringComparison.OrdinalIgnoreCase))
        {
            if (!template.AllowNewRecordCreate)
                throw new InvalidOperationException("This request type does not allow new record requests.");
            if (string.IsNullOrWhiteSpace(recordKey))
                throw new InvalidOperationException("Enter the new primary key for the new record.");
        }

        var request = new NavEditRequest
        {
            RequestTypeId = input.RequestTypeId,
            RecordKey = recordKey,
            RequestBody = input.RequestBody,
            UserId = userId,
            UserFullName = userFullName,
            Remark = input.Remark,
            CreatedAt = DateTime.UtcNow
        };

        if (template.Approvals is not { Count: > 0 })
        {
            request.Status = NavEditStatus.Pending;
            _db.Requests.Add(request);
            await _db.SaveChangesAsync(ct);
            await NotifySubmitItAdminAndOptionalAsync(template.ItAdminUserId, null, request, reqType, ct, null);
            return request;
        }

        var record = await BuildRecordForApprovalAsync(reqType, recordKey, input.RequestBody, reqMode, sourceRecordKey, ct);
        if (record == null)
        {
            if (string.Equals(reqMode, "create", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Could not build record for approval.");
            throw new InvalidOperationException($"Navision record not found for key \"{recordKey}\".");
        }

        var filteredSteps = new List<(List<string> UserIds, int OriginalIndex)>();
        for (var i = 0; i < template.Approvals.Count; i++)
        {
            var step = template.Approvals[i];
            if (!MatchesWhen(record, step.When))
                continue;

            var ids = NormalizeUserIds(step.UserIds);
            if (ids.Count == 0)
                throw new InvalidOperationException($"Approval step {i + 1} has no userIds (or empty list).");

            filteredSteps.Add((ids, i));
        }

        if (filteredSteps.Count == 0)
        {
            request.Status = NavEditStatus.Pending;
            _db.Requests.Add(request);
            await _db.SaveChangesAsync(ct);
            await NotifySubmitItAdminAndOptionalAsync(template.ItAdminUserId, null, request, reqType, ct, null);
            return request;
        }

        request.Status = NavEditStatus.PendingApproval;
        var level = 1;
        foreach (var (userIds, _) in filteredSteps)
        {
            var json = JsonSerializer.Serialize(userIds);
            request.Approvals.Add(new NavEditApproval
            {
                Level = level++,
                Role = string.Empty,
                RoleLabel = null,
                ApproverUserIdsJson = json,
                Status = ApprovalStatus.Pending,
                CreatedAt = DateTime.UtcNow
            });
        }

        _db.Requests.Add(request);
        await _db.SaveChangesAsync(ct);

        var firstLevelIds = filteredSteps[0].UserIds;
        await NotifySubmitItAdminAndOptionalAsync(template.ItAdminUserId, firstLevelIds, request, reqType, ct, 1);

        return request;
    }

    public async Task<bool> ResendSubmitNotificationsAsync(Guid requestId, string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return false;

        var request = await _db.Requests
            .Include(r => r.RequestType)
            .Include(r => r.Approvals)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);

        if (request == null || !string.Equals(request.UserId, userId, StringComparison.OrdinalIgnoreCase))
            return false;

        if (request.Status != NavEditStatus.Pending && request.Status != NavEditStatus.PendingApproval)
            return false;

        var reqType = request.RequestType ?? await _db.RequestTypes.FindAsync([request.RequestTypeId], ct);
        if (reqType == null) return false;

        var template = ParseTemplate(reqType.FieldsJson);

        List<string>? approverIds = null;
        int? levelForMessage = null;
        if (request.Status == NavEditStatus.PendingApproval)
        {
            var next = GetNextPendingApproval(request);
            if (next != null)
            {
                approverIds = ParseApproverUserIdsFromEntity(next);
                levelForMessage = next.Level;
            }
        }

        await NotifySubmitItAdminAndOptionalAsync(template.ItAdminUserId, approverIds, request, reqType, ct, levelForMessage);
        return true;
    }

    public async Task<IReadOnlyList<NavEditRequestDto>> GetMyRequestsAsync(string userId, CancellationToken ct = default)
    {
        var requests = await _db.Requests
            .Include(r => r.RequestType)
            .Include(r => r.Approvals)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return requests.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<NavEditRequestDto>> GetAllRequestsAsync(NavEditStatus? statusFilter = null, CancellationToken ct = default)
    {
        var query = _db.Requests
            .Include(r => r.RequestType)
            .Include(r => r.Approvals)
            .AsQueryable();

        if (statusFilter.HasValue)
            query = query.Where(r => r.Status == statusFilter.Value);

        var requests = await query.OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
        return requests.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<NavEditRequestDto>> GetPendingApprovalsAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(userId)) return Array.Empty<NavEditRequestDto>();

        var requests = await _db.Requests
            .Include(r => r.RequestType)
            .Include(r => r.Approvals)
            .Where(r => r.Status == NavEditStatus.PendingApproval)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        var filtered = new List<NavEditRequest>();
        foreach (var r in requests)
        {
            var next = GetNextPendingApproval(r);
            if (next == null) continue;
            var allowed = ParseApproverUserIdsFromEntity(next);
            if (allowed.Count == 0) continue;
            if (!allowed.Contains(userId, StringComparer.OrdinalIgnoreCase)) continue;
            filtered.Add(r);
        }

        return filtered.Select(ToDto).ToList();
    }

    public async Task<NavEditRequestDto?> GetRequestByIdAsync(Guid requestId, CancellationToken ct = default)
    {
        var request = await _db.Requests
            .Include(r => r.RequestType)
            .Include(r => r.Approvals.OrderBy(a => a.Level))
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);

        return request != null ? ToDto(request) : null;
    }

    // ── Actions ─────────────────────────────────────────────────

    public async Task<bool> ApproveRequestAsync(Guid requestId, int level, string userId, string? comment, CancellationToken ct = default)
    {
        var request = await _db.Requests
            .Include(r => r.Approvals)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);

        if (request == null || request.Status != NavEditStatus.PendingApproval)
            return false;

        var next = GetNextPendingApproval(request);
        if (next == null || next.Level != level)
            return false;

        if (!CanUserActOnApproval(next, userId))
            return false;

        next.Status = ApprovalStatus.Approved;
        next.ApprovedBy = userId;
        next.Comment = comment;
        next.ActionDate = DateTime.UtcNow;

        var ordered = request.Approvals.OrderBy(a => a.Level).ToList();
        var idx = ordered.FindIndex(a => a.Level == level);
        NavEditApproval? nextPending = idx >= 0 && idx + 1 < ordered.Count ? ordered[idx + 1] : null;

        var allApproved = request.Approvals.All(a => a.Status == ApprovalStatus.Approved);
        if (allApproved)
        {
            request.Status = NavEditStatus.Approved;
            request.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await NotifyRequesterAsync(request, "Navision edit approved",
                $"Your request for {request.RecordKey} ({request.RequestType?.Name ?? "edit"}) is fully approved.",
                "/navision-edits", ct);
            return true;
        }

        request.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        if (nextPending != null)
        {
            var ids = ParseApproverUserIdsFromEntity(nextPending);
            await NotifyUsersDistinctAsync(ids, "Navision edit — your approval needed",
                $"Request for record {request.RecordKey} ({request.RequestType?.Name ?? "edit"}) awaits level {nextPending.Level} approval.",
                "/navision-edits-admin", ct);
        }

        return true;
    }

    public async Task<bool> RejectRequestAsync(Guid requestId, string userId, string? comment, bool isApproval = false, int level = 0, CancellationToken ct = default)
    {
        var request = await _db.Requests
            .Include(r => r.RequestType)
            .Include(r => r.Approvals)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);

        if (request == null) return false;

        if (isApproval && level > 0)
        {
            var next = GetNextPendingApproval(request);
            if (next == null || next.Level != level)
                return false;
            if (!CanUserActOnApproval(next, userId))
                return false;

            next.Status = ApprovalStatus.Rejected;
            next.ApprovedBy = userId;
            next.Comment = comment;
            next.ActionDate = DateTime.UtcNow;
        }

        request.Status = NavEditStatus.Rejected;
        request.AdminRemark = comment;
        request.ProcessedBy = userId;
        request.ProcessedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await NotifyRequesterAsync(request, "Navision edit rejected",
            $"Your request for {request.RecordKey} ({request.RequestType?.Name ?? "edit"}) was rejected.",
            "/navision-edits", ct);

        return true;
    }

    public async Task<bool> ProcessRequestAsync(Guid requestId, string adminUserId, string? adminRemark, CancellationToken ct = default)
    {
        var request = await _db.Requests
            .Include(r => r.RequestType)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);
        if (request == null) return false;

        if (request.Status != NavEditStatus.Pending && request.Status != NavEditStatus.Approved)
            return false;

        var reqType = request.RequestType ?? await _db.RequestTypes.FindAsync([request.RequestTypeId], ct);
        if (reqType == null) return false;

        var template = ParseTemplate(reqType.FieldsJson);
        var changeMap = BuildChangeMapFromRequestBody(request.RequestBody);

        if (ShouldRunNavConnector(template))
        {
            var navOk = await TryExecuteNavConnectorAsync(template, request, changeMap, ct);
            if (!navOk)
            {
                _logger.LogWarning(
                    "NAV connector returned false for NavEdit request {RequestId} (process={Process})",
                    requestId,
                    template.ConnectorProcess);
                return false;
            }
        }

        request.Status = NavEditStatus.Processed;
        request.ProcessedBy = adminUserId;
        request.ProcessedAt = DateTime.UtcNow;
        request.AdminRemark = adminRemark;
        request.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        var processedMsg = ShouldRunNavConnector(template)
            ? $"Your request for {request.RecordKey} ({request.RequestType?.Name ?? "edit"}) was applied in NAV."
            : $"Your request for {request.RecordKey} ({request.RequestType?.Name ?? "edit"}) has been marked processed.";

        await NotifyRequesterAsync(request, "Navision edit processed", processedMsg, "/navision-edits", ct);

        return true;
    }

    /// <summary>Template-driven NAV WebServe Req* when <c>connectorProcess</c> is set (not none).</summary>
    private static bool ShouldRunNavConnector(FieldsTemplate template)
    {
        var p = template.ConnectorProcess?.Trim();
        if (string.IsNullOrEmpty(p)) return false;
        return !string.Equals(p, NavEditConnectorProcess.None, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> TryExecuteNavConnectorAsync(
        FieldsTemplate template,
        NavEditRequest request,
        IReadOnlyDictionary<string, string> changeMap,
        CancellationToken ct)
    {
        var p = template.ConnectorProcess?.Trim() ?? "";
        if (string.Equals(p, NavEditConnectorProcess.ReqCustEdit, StringComparison.OrdinalIgnoreCase))
            return await ExecuteReqCustEditAsync(template, request, changeMap, ct);

        if (string.Equals(p, NavEditConnectorProcess.ReqUserSetup, StringComparison.OrdinalIgnoreCase))
            return await ExecuteReqUserSetupAsync(template, request, changeMap, ct);

        if (string.Equals(p, NavEditConnectorProcess.ReqGlEntry, StringComparison.OrdinalIgnoreCase))
            return await ExecuteReqGlEntryAsync(template, request, changeMap, ct);

        throw new InvalidOperationException($"Unknown connectorProcess \"{p}\". Use none, reqCustEdit, reqUserSetup, or reqGlEntry.");
    }

    private async Task<bool> ExecuteReqCustEditAsync(
        FieldsTemplate template,
        NavEditRequest request,
        IReadOnlyDictionary<string, string> changeMap,
        CancellationToken ct)
    {
        var customerNo = ResolveConnectorParam(template, changeMap, "customerNo", "No_")?.Trim()
            ?? request.RecordKey?.Trim();
        if (string.IsNullOrWhiteSpace(customerNo))
            throw new InvalidOperationException("ReqCustEdit: missing customer number (record key or mapped column).");

        var dealerCode = ResolveConnectorParam(template, changeMap, "dealerCode", "Dealer Code", "DealerCode")?.Trim();
        var areaCode = ResolveConnectorParam(template, changeMap, "areaCode", "Area Code", "Area Code")?.Trim();

        if (string.IsNullOrWhiteSpace(dealerCode))
        {
            var reqTypeDealer = request.RequestType ?? await _db.RequestTypes.FindAsync([request.RequestTypeId], ct);
            var keyDealerLookup = request.RecordKey?.Trim() ?? customerNo;
            if (reqTypeDealer != null && !string.IsNullOrWhiteSpace(keyDealerLookup))
            {
                var dealerColsToTry = new List<string>();
                var mappedDealer = GetConnectorMappedColumnName(template, "dealerCode", "");
                if (!string.IsNullOrWhiteSpace(mappedDealer))
                    dealerColsToTry.Add(mappedDealer);
                foreach (var n in new[] { "Dealer Code", "DealerCode" })
                {
                    if (!dealerColsToTry.Exists(x => string.Equals(x, n, StringComparison.OrdinalIgnoreCase)))
                        dealerColsToTry.Add(n);
                }

                foreach (var col in dealerColsToTry)
                {
                    var live = (await GetLiveNavColumnValueAsync(reqTypeDealer, keyDealerLookup, col, ct))?.Trim();
                    if (!string.IsNullOrWhiteSpace(live))
                    {
                        dealerCode = live;

                        _logger.LogInformation(
                            "ReqCustEdit: dealer code filled from Nav live row (column={Column}, requestId={RequestId})",
                            col,
                            request.Id);
                        break;
                    }
                }
            }
        }

        if (string.IsNullOrWhiteSpace(dealerCode))
            throw new InvalidOperationException("ReqCustEdit: missing dealer code (set \"Dealer Code\" in changes or connectorParamColumns.dealerCode, or ensure Nav has the row/column).");

        if (string.IsNullOrWhiteSpace(areaCode))
        {
            var reqType = request.RequestType ?? await _db.RequestTypes.FindAsync([request.RequestTypeId], ct);
            var keyForLookup = request.RecordKey?.Trim() ?? customerNo;
            if (reqType != null && !string.IsNullOrWhiteSpace(keyForLookup))
            {
                var namesToTry = new List<string>();
                var mapped = GetConnectorMappedColumnName(template, "areaCode", "");
                if (!string.IsNullOrWhiteSpace(mapped))
                    namesToTry.Add(mapped);
                foreach (var n in new[] { "Area Code", "Area" })
                {
                    if (!namesToTry.Exists(x => string.Equals(x, n, StringComparison.OrdinalIgnoreCase)))
                        namesToTry.Add(n);
                }

                foreach (var col in namesToTry)
                {
                    var live = (await GetLiveNavColumnValueAsync(reqType, keyForLookup, col, ct))?.Trim();
                    if (!string.IsNullOrWhiteSpace(live))
                    {
                        areaCode = live;

                        _logger.LogInformation(
                            "ReqCustEdit: area code filled from Nav live row (column={Column}, requestId={RequestId})",
                            col,
                            request.Id);
                        break;
                    }
                }
            }
        }

        if (string.IsNullOrWhiteSpace(areaCode))
        {
            throw new InvalidOperationException("ReqCustEdit: missing area code (set \"Area Code\" in changes, connectorParamColumns.areaCode, or ensure Nav has the row/column).");
        }

        _logger.LogInformation(
            "ReqCustEdit: customerNo length={CLen}, dealer length={DLen}, area length={ALen}",
            customerNo.Length,
            dealerCode.Length,
            areaCode.Length);

        ct.ThrowIfCancellationRequested();
        return await _connector.ReqCustEditAsync(customerNo, dealerCode, areaCode);
    }

    private async Task<bool> ExecuteReqUserSetupAsync(
        FieldsTemplate template,
        NavEditRequest request,
        IReadOnlyDictionary<string, string> changeMap,
        CancellationToken ct)
    {
        var userid = ResolveConnectorParam(template, changeMap, "userid", "User ID", "User ID")?.Trim()
            ?? request.RecordKey?.Trim();
        if (string.IsNullOrWhiteSpace(userid))
            throw new InvalidOperationException("ReqUserSetup: missing user id (record key or connectorParamColumns.userid).");

        var respCenter = ResolveConnectorParam(template, changeMap, "respCenter", "Responsibility Center", "Resp_ Center", "Global Dimension 1 Code")?.Trim();

        if (string.IsNullOrWhiteSpace(respCenter))
        {
            var reqTypeRc = request.RequestType ?? await _db.RequestTypes.FindAsync([request.RequestTypeId], ct);
            var keyRcLookup = request.RecordKey?.Trim() ?? userid;
            if (reqTypeRc != null && !string.IsNullOrWhiteSpace(keyRcLookup))
            {
                var respColsToTry = new List<string>();
                var mappedRc = GetConnectorMappedColumnName(template, "respCenter", "");
                if (!string.IsNullOrWhiteSpace(mappedRc))
                    respColsToTry.Add(mappedRc);
                foreach (var n in new[] { "Responsibility Center", "Resp_ Center", "Global Dimension 1 Code" })
                {
                    if (!respColsToTry.Exists(x => string.Equals(x, n, StringComparison.OrdinalIgnoreCase)))
                        respColsToTry.Add(n);
                }

                foreach (var col in respColsToTry)
                {
                    var live = (await GetLiveNavColumnValueAsync(reqTypeRc, keyRcLookup, col, ct))?.Trim();
                    if (!string.IsNullOrWhiteSpace(live))
                    {
                        respCenter = live;

                        _logger.LogInformation(
                            "ReqUserSetup: responsibility center filled from Nav live row (column={Column}, requestId={RequestId})",
                            col,
                            request.Id);
                        break;
                    }
                }
            }
        }

        if (string.IsNullOrWhiteSpace(respCenter))
            throw new InvalidOperationException("ReqUserSetup: missing responsibility center in changes or mapping, or ensure Nav has the row/column.");

        var fromStr = ResolveConnectorParam(template, changeMap, "fromDate", "Work Date From", "From Date", "Valid From");
        var toStr = ResolveConnectorParam(template, changeMap, "toDate", "Work Date To", "To Date", "Valid To");

        if (string.IsNullOrWhiteSpace(fromStr) || string.IsNullOrWhiteSpace(toStr))
        {
            var reqTypeDt = request.RequestType ?? await _db.RequestTypes.FindAsync([request.RequestTypeId], ct);
            var keyDtLookup = request.RecordKey?.Trim() ?? userid;
            if (reqTypeDt != null && !string.IsNullOrWhiteSpace(keyDtLookup))
            {
                if (string.IsNullOrWhiteSpace(fromStr))
                {
                    var fromCols = new List<string>();
                    var mappedFrom = GetConnectorMappedColumnName(template, "fromDate", "");
                    if (!string.IsNullOrWhiteSpace(mappedFrom))
                        fromCols.Add(mappedFrom);
                    foreach (var n in new[] { "Work Date From", "From Date", "Valid From" })
                    {
                        if (!fromCols.Exists(x => string.Equals(x, n, StringComparison.OrdinalIgnoreCase)))
                            fromCols.Add(n);
                    }

                    foreach (var col in fromCols)
                    {
                        var live = (await GetLiveNavColumnValueAsync(reqTypeDt, keyDtLookup, col, ct))?.Trim();
                        if (!string.IsNullOrWhiteSpace(live))
                        {
                            fromStr = live;
                            _logger.LogInformation(
                                "ReqUserSetup: from date filled from Nav live row (column={Column}, requestId={RequestId})",
                                col,
                                request.Id);
                            break;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(toStr))
                {
                    var toCols = new List<string>();
                    var mappedTo = GetConnectorMappedColumnName(template, "toDate", "");
                    if (!string.IsNullOrWhiteSpace(mappedTo))
                        toCols.Add(mappedTo);
                    foreach (var n in new[] { "Work Date To", "To Date", "Valid To" })
                    {
                        if (!toCols.Exists(x => string.Equals(x, n, StringComparison.OrdinalIgnoreCase)))
                            toCols.Add(n);
                    }

                    foreach (var col in toCols)
                    {
                        var live = (await GetLiveNavColumnValueAsync(reqTypeDt, keyDtLookup, col, ct))?.Trim();
                        if (!string.IsNullOrWhiteSpace(live))
                        {
                            toStr = live;
                            _logger.LogInformation(
                                "ReqUserSetup: to date filled from Nav live row (column={Column}, requestId={RequestId})",
                                col,
                                request.Id);
                            break;
                        }
                    }
                }
            }
        }

        if (string.IsNullOrWhiteSpace(fromStr) || string.IsNullOrWhiteSpace(toStr))
            throw new InvalidOperationException("ReqUserSetup: missing from/to date columns in changes or connectorParamColumns, or ensure Nav has the row/column.");

        if (!TryParseNavDate(fromStr, out var fromDate) || !TryParseNavDate(toStr, out var toDate))
            throw new InvalidOperationException("ReqUserSetup: could not parse from/to dates.");

        _logger.LogInformation(
            "ReqUserSetup: userid length={ULen}, respCenter length={RLen}, from={From:O}, to={To:O}",
            userid.Length,
            respCenter.Length,
            fromDate,
            toDate);

        ct.ThrowIfCancellationRequested();
        return await _connector.ReqUserSetupAsync(userid, respCenter, fromDate, toDate);
    }

    private async Task<bool> ExecuteReqGlEntryAsync(
        FieldsTemplate template,
        NavEditRequest request,
        IReadOnlyDictionary<string, string> changeMap,
        CancellationToken ct)
    {
        var entryKey = ResolveConnectorParam(template, changeMap, "entryNo", "Entry No_")?.Trim()
            ?? request.RecordKey?.Trim();
        if (string.IsNullOrWhiteSpace(entryKey) || !int.TryParse(entryKey, NumberStyles.Integer, CultureInfo.InvariantCulture, out var entryNo))
            throw new InvalidOperationException("ReqGLEntry: entry number must be an integer (record key or Entry No_ column).");

        var glAccountNo = ResolveConnectorParam(template, changeMap, "glAccountNo", "G_L Account No_", "G/L Account No_", "G_L Account No_")?.Trim();
        var respCenter = ResolveConnectorParam(template, changeMap, "respCenter", "Responsibility Center", "Resp_ Center")?.Trim();
        var amountStr = ResolveConnectorParam(template, changeMap, "amount", "Amount");
        var postingStr = ResolveConnectorParam(template, changeMap, "postingDate", "Posting Date");

        if (string.IsNullOrWhiteSpace(glAccountNo))
            throw new InvalidOperationException("ReqGLEntry: missing G/L account (changes or connectorParamColumns.glAccountNo).");
        if (string.IsNullOrWhiteSpace(respCenter))
            throw new InvalidOperationException("ReqGLEntry: missing responsibility center.");
        if (string.IsNullOrWhiteSpace(amountStr) || !decimal.TryParse(amountStr.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
            throw new InvalidOperationException("ReqGLEntry: missing or invalid amount.");
        if (string.IsNullOrWhiteSpace(postingStr) || !TryParseNavDate(postingStr, out var postingDate))
            throw new InvalidOperationException("ReqGLEntry: missing or invalid posting date.");

        _logger.LogInformation(
            "ReqGLEntry: entryNo={EntryNo}, glAccountNo length={GLen}, respCenter length={RLen}, amount={Amount}",
            entryNo,
            glAccountNo.Length,
            respCenter.Length,
            amount);

        ct.ThrowIfCancellationRequested();
        return await _connector.ReqGLEntryAsync(entryNo, glAccountNo, respCenter, amount, postingDate);
    }

    private static bool TryParseNavDate(string? s, out DateTime dt)
    {
        dt = default;
        if (string.IsNullOrWhiteSpace(s)) return false;
        var t = s.Trim();
        if (DateTime.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out dt)) return true;
        if (DateTime.TryParse(t, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out dt)) return true;
        return false;
    }

    /// <summary>Resolve a value from explicit JSON mapping, then change map by fallback column names.</summary>
    private static string? ResolveConnectorParam(
        FieldsTemplate template,
        IReadOnlyDictionary<string, string> changeMap,
        string logicalKey,
        params string[] defaultFallbackColumns)
    {
        if (template.ConnectorParamColumns != null)
        {
            string? mappedCol = null;
            if (template.ConnectorParamColumns.TryGetValue(logicalKey, out var mc))
                mappedCol = mc;
            else
            {
                foreach (var kv in template.ConnectorParamColumns)
                {
                    if (string.Equals(kv.Key, logicalKey, StringComparison.OrdinalIgnoreCase))
                    {
                        mappedCol = kv.Value;
                        break;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(mappedCol))
            {
                var key = mappedCol!.Trim();
                if (changeMap.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
                    return v;
            }
        }

        foreach (var col in defaultFallbackColumns)
        {
            if (string.IsNullOrWhiteSpace(col)) continue;
            if (changeMap.TryGetValue(col, out var v2) && !string.IsNullOrWhiteSpace(v2))
                return v2;
        }

        return null;
    }

    private static Dictionary<string, string> BuildChangeMapFromRequestBody(string? requestBody)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (col, nv) in ParseChangeListFromRequestBody(requestBody))
        {
            if (string.IsNullOrWhiteSpace(col)) continue;
            d[col] = nv ?? "";
        }

        return d;
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static NavEditApproval? GetNextPendingApproval(NavEditRequest request)
    {
        foreach (var a in request.Approvals.OrderBy(x => x.Level))
        {
            if (a.Status == ApprovalStatus.Pending)
                return a;
        }

        return null;
    }

    private static bool CanUserActOnApproval(NavEditApproval approval, string userId)
    {
        var allowed = ParseApproverUserIdsFromEntity(approval);
        if (allowed.Count == 0)
            return true;
        return allowed.Contains(userId, StringComparer.OrdinalIgnoreCase);
    }

    private static List<string> ParseApproverUserIdsFromEntity(NavEditApproval approval)
    {
        if (string.IsNullOrWhiteSpace(approval.ApproverUserIdsJson))
            return new List<string>();
        try
        {
            var arr = JsonSerializer.Deserialize<List<string>>(approval.ApproverUserIdsJson, JsonOpts);
            return arr?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private async Task NotifyRequesterAsync(NavEditRequest request, string title, string message, string link, CancellationToken ct)
    {
        try
        {
            await _notificationService.SendNotificationAsync(request.UserId, title, message, NotificationType.Info, link, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Navision edit notification to requester {UserId} failed", request.UserId);
        }
    }

    private async Task NotifySubmitItAdminAndOptionalAsync(
        string? itAdminUserId,
        List<string>? approverIds,
        NavEditRequest request,
        NavEditRequestType reqType,
        CancellationToken ct,
        int? approvalLevelForMessage)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        async Task OneAsync(string uid, string title, string msg, string link)
        {
            if (string.IsNullOrWhiteSpace(uid) || !seen.Add(uid)) return;
            try
            {
                await _notificationService.SendNotificationAsync(uid.Trim(), title, msg, NotificationType.Info, link, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Navision edit notification to {UserId} failed", uid);
            }
        }

        var typeName = reqType.Name;
        var msgBase = $"New Navision edit request for {request.RecordKey} ({typeName}).";

        if (!string.IsNullOrWhiteSpace(itAdminUserId))
            await OneAsync(itAdminUserId!, "Navision edit — new request", msgBase, "/navision-edits-admin");

        if (approverIds is { Count: > 0 })
        {
            var lvl = approvalLevelForMessage ?? 1;
            foreach (var uid in approverIds)
                await OneAsync(uid, "Navision edit — approval needed", msgBase + $" Your approval is required at level {lvl}.", "/navision-edits-admin");
        }
    }

    private async Task NotifyUsersDistinctAsync(IReadOnlyList<string> userIds, string title, string message, string link, CancellationToken ct)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var uid in userIds)
        {
            if (string.IsNullOrWhiteSpace(uid) || !seen.Add(uid)) continue;
            try
            {
                await _notificationService.SendNotificationAsync(uid.Trim(), title, message, NotificationType.Info, link, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Navision edit notification to {UserId} failed", uid);
            }
        }
    }

    private static NavEditRequestDto ToDto(NavEditRequest r) => new()
    {
        Id = r.Id,
        RequestTypeId = r.RequestTypeId,
        RequestTypeName = r.RequestType?.Name ?? "",
        RequestTypeCode = r.RequestType?.Code ?? "",
        RequestTypeIcon = r.RequestType?.Icon,
        RecordKey = r.RecordKey,
        RequestBody = r.RequestBody,
        UserId = r.UserId,
        UserFullName = r.UserFullName,
        Status = (int)r.Status,
        StatusLabel = r.Status.ToString(),
        Remark = r.Remark,
        AdminRemark = r.AdminRemark,
        ProcessedBy = r.ProcessedBy,
        ProcessedAt = r.ProcessedAt,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt,
        Approvals = r.Approvals.OrderBy(a => a.Level).Select(a =>
        {
            var ids = ParseApproverUserIdsFromSnapshot(a.ApproverUserIdsJson);
            return new NavEditApprovalDto
            {
                Id = a.Id,
                Level = a.Level,
                Role = a.Role,
                RoleLabel = a.RoleLabel,
                ApproverUserIds = ids,
                ApprovedBy = a.ApprovedBy,
                Status = (int)a.Status,
                StatusLabel = a.Status.ToString(),
                Comment = a.Comment,
                ActionDate = a.ActionDate
            };
        }).ToList()
    };

    private static List<string> ParseApproverUserIdsFromSnapshot(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try
        {
            var arr = JsonSerializer.Deserialize<List<string>>(json, JsonOpts);
            return arr?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList() ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static List<string> NormalizeUserIds(IReadOnlyList<string>? userIds)
    {
        if (userIds == null || userIds.Count == 0) return new List<string>();
        return userIds.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool MatchesWhen(Dictionary<string, object?> record, List<ConditionDef>? when)
    {
        if (when == null || when.Count == 0) return true;
        foreach (var c in when)
        {
            if (string.IsNullOrEmpty(c.Column)) return false;
            if (!EvaluateCondition(record, c)) return false;
        }

        return true;
    }

    private static bool EvaluateCondition(Dictionary<string, object?> record, ConditionDef c)
    {
        var raw = GetRecordValue(record, c.Column);
        var cell = NormalizeCell(raw);
        var op = (c.Op ?? "eq").Trim().ToLowerInvariant();

        switch (op)
        {
            case "eq":
                return string.Equals(cell, NormalizeCell(c.Value), StringComparison.OrdinalIgnoreCase);
            case "neq":
                return !string.Equals(cell, NormalizeCell(c.Value), StringComparison.OrdinalIgnoreCase);
            case "in":
            {
                var set = (c.Values ?? new List<string>()).Select(NormalizeCell).ToHashSet(StringComparer.OrdinalIgnoreCase);
                return set.Contains(cell);
            }
            case "nin":
            {
                var set = (c.Values ?? new List<string>()).Select(NormalizeCell).ToHashSet(StringComparer.OrdinalIgnoreCase);
                return !set.Contains(cell);
            }
            default:
                return false;
        }
    }

    private static string? GetRecordValue(Dictionary<string, object?> record, string column)
    {
        if (record.TryGetValue(column, out var v)) return v?.ToString();
        var key = record.Keys.FirstOrDefault(k => string.Equals(k, column, StringComparison.OrdinalIgnoreCase));
        return key != null ? record[key]?.ToString() : null;
    }

    private static string NormalizeCell(string? s) => s?.Trim() ?? "";

    private static string NormalizeCell(object? o) => o?.ToString()?.Trim() ?? "";

    /// <summary>
    /// Reads <c>displayColumns</c> when the main deserializer leaves it empty (e.g. converter/name edge cases).
    /// Matches the template JSON shape produced by the admin UI.
    /// </summary>
    private static List<DisplayColumnSpec>? TryParseDisplayColumnsFromJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!TryGetJsonPropertyIgnoreCase(doc.RootElement, "displayColumns", out var dc)
                || dc.ValueKind != JsonValueKind.Array)
                return null;
            var list = new List<DisplayColumnSpec>();
            foreach (var item in dc.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                    list.Add(new DisplayColumnSpec { Column = item.GetString() ?? "", Format = null });
                else if (item.ValueKind == JsonValueKind.Object)
                {
                    string? col = null;
                    string? fmt = null;
                    if (TryGetJsonPropertyIgnoreCase(item, "column", out var c) && c.ValueKind == JsonValueKind.String)
                        col = c.GetString();
                    if (TryGetJsonPropertyIgnoreCase(item, "format", out var f) && f.ValueKind == JsonValueKind.String)
                        fmt = f.GetString();
                    list.Add(new DisplayColumnSpec { Column = col ?? "", Format = string.IsNullOrWhiteSpace(fmt) ? null : fmt });
                }
            }
            return list;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryGetJsonPropertyIgnoreCase(JsonElement obj, string name, out JsonElement value)
    {
        value = default;
        if (obj.ValueKind != JsonValueKind.Object) return false;
        foreach (var p in obj.EnumerateObject())
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = p.Value;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Builds the display column list for lookup SELECT: non-empty template specs, else raw JSON parse, else PK only.
    /// </summary>
    private static List<DisplayColumnSpec> ResolveDisplayColumnsForLookup(
        FieldsTemplate template,
        string rawFieldsJson,
        string primaryKeyColumn)
    {
        var displayColumns = template.DisplayColumns?
            .Where(s => !string.IsNullOrWhiteSpace(s.Column))
            .ToList() ?? new List<DisplayColumnSpec>();
        if (displayColumns.Count == 0)
        {
            var parsed = TryParseDisplayColumnsFromJson(rawFieldsJson);
            if (parsed != null)
                displayColumns = parsed.Where(s => !string.IsNullOrWhiteSpace(s.Column)).ToList();
        }
        if (displayColumns.Count == 0)
            displayColumns = new List<DisplayColumnSpec> { new() { Column = primaryKeyColumn } };
        return displayColumns;
    }

    private static void TryParseRequestBodyMode(string? requestBody, out string mode, out string? sourceRecordKey)
    {
        mode = "edit";
        sourceRecordKey = null;
        if (string.IsNullOrWhiteSpace(requestBody)) return;
        try
        {
            using var doc = JsonDocument.Parse(requestBody);
            if (TryGetJsonPropertyIgnoreCase(doc.RootElement, "mode", out var m) && m.ValueKind == JsonValueKind.String)
            {
                var s = (m.GetString() ?? "").Trim().ToLowerInvariant();
                if (s is "create" or "edit") mode = s;
            }
            if (TryGetJsonPropertyIgnoreCase(doc.RootElement, "sourceRecordKey", out var sk) && sk.ValueKind == JsonValueKind.String)
                sourceRecordKey = sk.GetString()?.Trim();
        }
        catch
        {
            // ignore malformed JSON; treat as edit
        }
    }

    private static List<(string Column, string? NewValue)> ParseChangeListFromRequestBody(string? requestBody)
    {
        var list = new List<(string, string?)>();
        if (string.IsNullOrWhiteSpace(requestBody)) return list;
        try
        {
            using var doc = JsonDocument.Parse(requestBody);
            if (!TryGetJsonPropertyIgnoreCase(doc.RootElement, "changes", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return list;
            foreach (var el in arr.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object) continue;
                string col = "";
                string? nv = null;
                if (TryGetJsonPropertyIgnoreCase(el, "column", out var c) && c.ValueKind == JsonValueKind.String)
                    col = c.GetString() ?? "";
                if (TryGetJsonPropertyIgnoreCase(el, "newValue", out var n))
                {
                    nv = n.ValueKind switch
                    {
                        JsonValueKind.String => n.GetString(),
                        JsonValueKind.Number => n.GetRawText(),
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        JsonValueKind.Null => "",
                        _ => n.GetRawText(),
                    };
                }
                if (!string.IsNullOrWhiteSpace(col))
                    list.Add((col, nv ?? ""));
            }
        }
        catch
        {
            // ignore
        }
        return list;
    }

    private async Task<Dictionary<string, object?>?> BuildRecordForApprovalAsync(
        NavEditRequestType reqType,
        string recordKey,
        string requestBody,
        string mode,
        string? sourceRecordKey,
        CancellationToken ct)
    {
        var id = reqType.Id;
        if (!string.Equals(mode, "create", StringComparison.OrdinalIgnoreCase))
            return await GetRecordByKeyAsync(id, recordKey, ct);

        Dictionary<string, object?> record;
        if (!string.IsNullOrWhiteSpace(sourceRecordKey))
        {
            var src = await GetRecordByKeyAsync(id, sourceRecordKey, ct);
            if (src == null)
                throw new InvalidOperationException($"Source record not found for key \"{sourceRecordKey}\".");
            record = new Dictionary<string, object?>(src, StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            record = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        foreach (var (col, newVal) in ParseChangeListFromRequestBody(requestBody))
        {
            if (string.IsNullOrWhiteSpace(col)) continue;
            record[col] = newVal;
        }

        return record;
    }

    private static FieldsTemplate ParseTemplate(string json)
    {
        json = json?.Trim() ?? "";
        if (string.IsNullOrEmpty(json))
            return new FieldsTemplate();
        try
        {
            var t = JsonSerializer.Deserialize<FieldsTemplate>(json, JsonOpts) ?? new FieldsTemplate();
            if (t.DisplayColumns == null || t.DisplayColumns.Count == 0)
            {
                var fallback = TryParseDisplayColumnsFromJson(json);
                if (fallback != null && fallback.Count > 0)
                    t.DisplayColumns = fallback;
            }
            else if (t.DisplayColumns.All(s => string.IsNullOrWhiteSpace(s.Column)))
            {
                var fallback = TryParseDisplayColumnsFromJson(json);
                if (fallback != null && fallback.Any(s => !string.IsNullOrWhiteSpace(s.Column)))
                    t.DisplayColumns = fallback;
            }
            return t;
        }
        catch
        {
            // Do not attach partial displayColumns here — GetRecord needs a consistent template.
            // Lookup still recovers display columns via ResolveDisplayColumnsForLookup + TryParseDisplayColumnsFromJson.
            return new FieldsTemplate();
        }
    }

    /// <summary>Sanitize SQL identifiers to prevent injection. Only allows alphanumeric, underscore, space, and period.</summary>
    private static string SanitizeColumnName(string name) =>
        new(name.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == ' ').ToArray());

    /// <summary>Navision company-prefixed table names may include parentheses, e.g. <c>Tyresoles (India) Pvt_ Ltd_$Customer</c>.</summary>
    private static string SanitizeTableName(string name) =>
        new(name.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == ' ' || c == '$' || c == '(' || c == ')').ToArray());
}

// ── Template JSON Model ─────────────────────────────────────────

internal sealed class DisplayColumnSpec
{
    public string Column { get; set; } = "";
    public string? Format { get; set; }
}

internal sealed class DisplayColumnListJsonConverter : System.Text.Json.Serialization.JsonConverter<List<DisplayColumnSpec>?>
{
    public override List<DisplayColumnSpec>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType != JsonTokenType.StartArray) return new List<DisplayColumnSpec>();
        var list = new List<DisplayColumnSpec>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray) break;
            if (reader.TokenType == JsonTokenType.String)
            {
                list.Add(new DisplayColumnSpec { Column = reader.GetString() ?? "", Format = null });
            }
            else if (reader.TokenType == JsonTokenType.StartObject)
            {
                string? col = null;
                string? fmt = null;
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject) break;
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;
                    var name = reader.GetString() ?? "";
                    reader.Read();
                    if (name.Equals("column", StringComparison.OrdinalIgnoreCase))
                    {
                        if (reader.TokenType == JsonTokenType.String)
                            col = reader.GetString();
                        else
                            reader.Skip();
                    }
                    else if (name.Equals("format", StringComparison.OrdinalIgnoreCase))
                    {
                        if (reader.TokenType == JsonTokenType.String)
                            fmt = reader.GetString();
                        else
                            reader.Skip();
                    }
                    else
                        reader.Skip();
                }
                list.Add(new DisplayColumnSpec { Column = col ?? "", Format = string.IsNullOrWhiteSpace(fmt) ? null : fmt });
            }
            else
            {
                reader.Skip();
            }
        }
        return list;
    }

    public override void Write(Utf8JsonWriter writer, List<DisplayColumnSpec>? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }
        writer.WriteStartArray();
        foreach (var v in value)
        {
            if (string.IsNullOrWhiteSpace(v.Format))
            {
                writer.WriteStringValue(v.Column ?? "");
            }
            else
            {
                writer.WriteStartObject();
                writer.WriteString("column", v.Column ?? "");
                writer.WriteString("format", v.Format!);
                writer.WriteEndObject();
            }
        }
        writer.WriteEndArray();
    }
}

internal sealed class LookupFilterSpec
{
    public string Column { get; set; } = "";
    public string? Op { get; set; }
    public string? Value { get; set; }
    public List<string>? Values { get; set; }
}

/// <summary>Values for <see cref="FieldsTemplate.ConnectorProcess"/> (template JSON <c>connectorProcess</c>).</summary>
internal static class NavEditConnectorProcess
{
    public const string None = "none";
    public const string ReqCustEdit = "reqCustEdit";
    public const string ReqUserSetup = "reqUserSetup";
    public const string ReqGlEntry = "reqGlEntry";
}

internal class FieldsTemplate
{
    [JsonPropertyName("itAdminUserId")]
    public string? ItAdminUserId { get; set; }
    [JsonPropertyName("fields")]
    public List<FieldDef>? Fields { get; set; }
    [JsonPropertyName("approvals")]
    public List<ApprovalDef>? Approvals { get; set; }
    [JsonPropertyName("displayColumns")]
    [JsonConverter(typeof(DisplayColumnListJsonConverter))]
    public List<DisplayColumnSpec>? DisplayColumns { get; set; }
    /// <summary>Nav columns for find-record substring search (OR). Empty = primary key only.</summary>
    [JsonPropertyName("searchColumns")]
    public List<string>? SearchColumns { get; set; }
    /// <summary>Static AND filters on lookup queries (template JSON <c>lookupFilters</c>).</summary>
    [JsonPropertyName("lookupFilters")]
    public List<LookupFilterSpec>? LookupFilters { get; set; }

    /// <summary>When true, users may submit <c>mode: "create"</c> with a new primary key and optional copy-from source.</summary>
    [JsonPropertyName("allowNewRecordCreate")]
    public bool AllowNewRecordCreate { get; set; }

    /// <summary>Admin "process" calls NAV WebServe Req* via <see cref="Connector"/>; <c>none</c> = mark processed only.</summary>
    [JsonPropertyName("connectorProcess")]
    public string? ConnectorProcess { get; set; }

    /// <summary>Optional map: logical param name (e.g. <c>dealerCode</c>) → Nav column name in <c>requestBody.changes</c>.</summary>
    [JsonPropertyName("connectorParamColumns")]
    public Dictionary<string, string>? ConnectorParamColumns { get; set; }
}

internal class FieldDef
{
    public string Column { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = "text";
    public List<string>? Options { get; set; }
}

internal class ApprovalDef
{
    /// <summary>Optional legacy level from JSON; order in array defines order after filtering.</summary>
    public int Level { get; set; }
    public List<string>? UserIds { get; set; }
    public List<ConditionDef>? When { get; set; }
}

internal class ConditionDef
{
    public string Column { get; set; } = string.Empty;
    public string? Op { get; set; }
    public string? Value { get; set; }
    public List<string>? Values { get; set; }
}
