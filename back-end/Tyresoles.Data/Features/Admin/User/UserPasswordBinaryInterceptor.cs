using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Text.RegularExpressions;
using Dataverse.NavLive;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Tyresoles.Sql.Abstractions;
using Tyresoles.Sql.Core.Metadata;

namespace Tyresoles.Data.Features.Admin.User;

/// <summary>
/// Intercepts User updates to ensure IMAGE/VARBINARY(MAX) columns are correctly typed when configured.
/// When UserPasswordBinaryOptions.ConvertPasswordColumnsToBinary is true, Salt and Encrypted Password
/// are sent as binary (for IMAGE columns). When false (default), they are sent as strings (NVARCHAR).
/// </summary>
public sealed class UserPasswordBinaryInterceptor : IDbCommandInterceptor, IParameterPreprocessor
{
    private static readonly Regex ParamAssignmentRegex = new(@"(?:\[([^\]]+)\]|(\w+))\s*=\s*(@p\d+)", RegexOptions.Compiled);

    private static readonly HashSet<string> PasswordColumnNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Encrypted Password",
        "Salt",
    };

    private readonly UserPasswordBinaryOptions _options;

    public UserPasswordBinaryInterceptor(IOptions<UserPasswordBinaryOptions> options)
    {
        _options = options?.Value ?? new UserPasswordBinaryOptions();
    }

    private bool IsBinaryColumn(PropertyInfo prop, string columnName)
    {
        if (prop.PropertyType == typeof(byte[])) return true;
        if (columnName.Contains("Picture", StringComparison.OrdinalIgnoreCase)) return true;
        if (PasswordColumnNames.Contains(columnName)) return _options.ConvertPasswordColumnsToBinary;
        if (string.Equals(columnName, "Tauri Config", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>Convert Base64 strings to byte[] only when ConvertPasswordColumnsToBinary is true.</summary>
    public IReadOnlyDictionary<string, object>? PreProcessParameters(string sql, IReadOnlyDictionary<string, object> parameters)
    {
        if (!_options.ConvertPasswordColumnsToBinary) return null;
        if (string.IsNullOrEmpty(sql) || !sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase) || !sql.Contains("User]", StringComparison.OrdinalIgnoreCase))
            return null;

        var dict = new Dictionary<string, object>(parameters);
        var changed = false;
        foreach (var key in dict.Keys.ToList())
        {
            if (dict[key] is string s && s.Length >= 20 && IsLikelyBase64(s))
            {
                try
                {
                    dict[key] = Convert.FromBase64String(s);
                    changed = true;
                }
                catch { }
            }
        }
        return changed ? dict : null;
    }

    public ValueTask OnBeforeExecuteAsync(DbCommand command, CancellationToken ct)
    {
        var sql = command.CommandText ?? "";
        if (!sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase) || !sql.Contains("User]", StringComparison.OrdinalIgnoreCase))
            return ValueTask.CompletedTask;

        if (command is not SqlCommand sqlCmd)
            return ValueTask.CompletedTask;

        // 1. Parse SQL to map parameter names to column names
        var paramToColumn = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var matches = ParamAssignmentRegex.Matches(sql);
        foreach (Match m in matches)
        {
            var col = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
            var param = m.Groups[3].Value;
            paramToColumn[param] = col;
        }

        // 2. Identify which parameters hit binary columns
        var mappedProps = EntityMetadata<Dataverse.NavLive.User>.MappedProperties.ToDictionary(m => m.ColumnName, m => m.Property, StringComparer.OrdinalIgnoreCase);
        
        var toConvert = new List<(string ParamName, object? Value)>();
        foreach (DbParameter p in sqlCmd.Parameters)
        {
            var pName = p.ParameterName ?? "";
            if (!paramToColumn.TryGetValue(pName, out var colName)) continue;
            
            if (!mappedProps.TryGetValue(colName, out var prop)) continue;

            if (IsBinaryColumn(prop, colName))
            {
                object? val = p.Value;
                if (val is string s && s.Length > 0)
                {
                    try { val = Convert.FromBase64String(s); } catch { }
                }
                toConvert.Add((pName, val));
            }
        }

        // 3. Re-bind parameters with explicit SqlDbType.Image or VarBinary
        foreach (var (pName, val) in toConvert)
        {
            var idx = sqlCmd.Parameters.IndexOf(pName);
            if (idx >= 0)
            {
                sqlCmd.Parameters.RemoveAt(idx);
                // forcing SqlDbType.Image is the most compatible fix for NAV's legacy BLOB fields
                sqlCmd.Parameters.Insert(idx, new SqlParameter(pName, SqlDbType.Image) { Value = val ?? DBNull.Value });
            }
        }

        return ValueTask.CompletedTask;
    }

    private static bool IsLikelyBase64(string s)
    {
        if (s.Length % 4 != 0) return false;
        return Regex.IsMatch(s, @"^[a-zA-Z0-9\+/]*={0,2}$");
    }
}


