using System.Data;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace Tyresoles.Sql.Cli;

public sealed class TableColumn
{
    public string ColumnName { get; set; } = "";
    public string SqlType { get; set; } = "";
    public bool IsNullable { get; set; }
    public string CSharpType => ModelGenerator.SqlTypeToCSharp(SqlType, IsNullable);
    public string PropertyName => ModelGenerator.SanitizePropertyName(ModelGenerator.ToPascalCase(ColumnName));
}

public sealed class TableSchema
{
    public string LogicalName { get; set; } = "";
    public string PhysicalName { get; set; } = "";
    public bool IsShared { get; set; }
    public List<TableColumn> Columns { get; set; } = new();
    public List<string> KeyColumns { get; set; } = new();
    public string ModelName => ModelGenerator.ToPascalCase(LogicalName);
}

public static class ModelGenerator
{
    private static readonly HashSet<string> CSharpKeywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue",
        "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally",
        "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params", "private", "protected",
        "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string",
        "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
        "using", "virtual", "void", "volatile", "while"
    };

    internal static string SanitizePropertyName(string name)
    {
        if (CSharpKeywords.Contains(name)) return "@" + name;
        return name;
    }

    public static string ToPascalCase(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Entity";
        var normalized = new string(name.Select(c =>
            char.IsLetterOrDigit(c) ? c : (c == ' ' || c == '_' ? c : ' ')).ToArray());
        var parts = normalized.Split(new[] { ' ', '_' }, StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            if (part.Length == 0) continue;
            if (part.Length == 1)
                sb.Append(char.ToUpperInvariant(part[0]));
            else if (part.All(char.IsUpper))
                sb.Append(part);
            else
                sb.Append(char.ToUpperInvariant(part[0])).Append(part.AsSpan(1).ToString().ToLowerInvariant());
        }
        var s = sb.ToString();
        if (s.Length > 0 && char.IsDigit(s[0])) s = "_" + s;
        return string.IsNullOrEmpty(s) ? "Entity" : s;
    }

    public static string SqlTypeToCSharp(string sqlType, bool isNullable)
    {
        var (baseType, forceNullable) = sqlType?.ToLowerInvariant() switch
        {
            "int" => ("int", false),
            "bigint" => ("long", false),
            "smallint" => ("short", false),
            "tinyint" => ("byte", false),
            "bit" => ("bool", false),
            "uniqueidentifier" => ("Guid", false),
            "datetime" or "datetime2" or "date" or "smalldatetime" => ("DateTime", true),
            "decimal" or "numeric" => ("decimal", false),
            "float" => ("double", false),
            "real" => ("float", false),
            "money" or "smallmoney" => ("decimal", false),
            "nvarchar" or "varchar" or "nchar" or "char" or "text" or "ntext" => ("string", false),
            "varbinary" or "binary" or "image" => ("byte[]", false),
            _ => ("object", true)
        };
        if (baseType == "string" || baseType == "byte[]" || baseType == "object")
            return baseType;
        return (isNullable || forceNullable) ? baseType + "?" : baseType;
    }

    public static string? GetSchemaSource(JsonElement root)
    {
        if (root.TryGetProperty("SchemaSource", out var ss))
            return ss.GetString();
        var first = root.EnumerateObject().FirstOrDefault(p =>
            !p.Name.Equals("SchemaSource", StringComparison.OrdinalIgnoreCase) &&
            p.Value.ValueKind == JsonValueKind.Object);
        return first.Value.ValueKind == JsonValueKind.Object ? first.Name : null;
    }

    public static string? GetConnectionString(JsonElement root)
    {
        var schemaSource = GetSchemaSource(root);
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Name.Equals("SchemaSource", StringComparison.OrdinalIgnoreCase))
                continue;
            if (schemaSource != null && !prop.Name.Equals(schemaSource, StringComparison.OrdinalIgnoreCase))
                continue;
            if (prop.Value.ValueKind != JsonValueKind.Object)
                continue;
            if (prop.Value.TryGetProperty("ConnectionString", out var cs))
                return cs.GetString();
        }
        return null;
    }

    public static List<TableSchema> LoadTableList(JsonElement root)
    {
        var allTables = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Name.Equals("SchemaSource", StringComparison.OrdinalIgnoreCase))
                continue;
            if (prop.Value.ValueKind != JsonValueKind.Object)
                continue;
            var tenant = prop.Value;
            if (tenant.TryGetProperty("Tables", out var tables))
                foreach (var t in tables.EnumerateArray())
                    allTables[t.GetString() ?? ""] = false;
            if (tenant.TryGetProperty("SharedTables", out var shared))
                foreach (var t in shared.EnumerateArray())
                    allTables[t.GetString() ?? ""] = true;
        }
        return allTables
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Key))
            .Select(kv => new TableSchema { LogicalName = kv.Key, IsShared = kv.Value })
            .OrderBy(t => t.LogicalName)
            .ToList();
    }

    public static string? ResolvePhysicalTableNameSafe(SqlConnection conn, string logicalName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT TOP 1 name FROM sys.tables WHERE name = @name OR name LIKE @pattern";
        cmd.Parameters.AddWithValue("@name", logicalName);
        cmd.Parameters.AddWithValue("@pattern", "%$" + logicalName);
        try
        {
            if (conn.State != ConnectionState.Open)
                conn.Open();
            var obj = cmd.ExecuteScalar();
            return obj?.ToString();
        }
        catch
        {
            return null;
        }
    }

    public static void FillSchema(SqlConnection conn, TableSchema table)
    {
        var physical = ResolvePhysicalTableNameSafe(conn, table.LogicalName);
        if (string.IsNullOrEmpty(physical))
        {
            table.PhysicalName = table.LogicalName;
            return;
        }
        table.PhysicalName = physical;

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT c.name, t.name AS type_name, c.is_nullable
                FROM sys.columns c
                INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
                WHERE c.object_id = OBJECT_ID(@table)
                ORDER BY c.column_id
                """;
            cmd.Parameters.AddWithValue("@table", physical);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                table.Columns.Add(new TableColumn
                {
                    ColumnName = r.GetString(0),
                    SqlType = r.GetString(1),
                    IsNullable = r.GetBoolean(2)
                });
            }
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT c.name
                FROM sys.index_columns ic
                INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                WHERE ic.object_id = OBJECT_ID(@table) AND ic.index_id = 1
                ORDER BY ic.key_ordinal
                """;
            cmd.Parameters.AddWithValue("@table", physical);
            try
            {
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    table.KeyColumns.Add(r.GetString(0));
            }
            catch
            {
                if (table.Columns.Count > 0)
                    table.KeyColumns.Add(table.Columns[0].ColumnName);
            }
        }

        if (table.KeyColumns.Count == 0 && table.Columns.Count > 0)
        {
            var first = table.Columns.FirstOrDefault(c =>
                c.ColumnName.Equals("No", StringComparison.OrdinalIgnoreCase) ||
                c.ColumnName.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
                c.ColumnName.Equals("Entry_No_", StringComparison.OrdinalIgnoreCase) ||
                c.ColumnName.Equals("Code", StringComparison.OrdinalIgnoreCase));
            if (first != null)
                table.KeyColumns.Add(first.ColumnName);
            else
                table.KeyColumns.Add(table.Columns[0].ColumnName);
        }
    }

    public static string GenerateCSharp(IEnumerable<TableSchema> tables, string modelNamespace)
    {
        var ns = string.IsNullOrWhiteSpace(modelNamespace) ? "Tyresoles.Generated" : modelNamespace.Trim();
        // Same logical table can appear more than once if config or merge artifacts duplicate entries;
        // keep the richest schema (most columns, then most key columns) so partial class names stay unique.
        var deduped = tables
            .GroupBy(t => t.ModelName, StringComparer.Ordinal)
            .Select(g => g
                .OrderByDescending(t => t.Columns.Count)
                .ThenByDescending(t => t.KeyColumns.Count)
                .First())
            .OrderBy(t => t.LogicalName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("// Auto-generated by tyresoles generate. Do not edit by hand.");
        sb.AppendLine("// Models for use with Tyresoles.Sql and Tyresoles.Data (Dataverse, Query<T>, WithCompany).");
        sb.AppendLine("using Tyresoles.Sql.Abstractions;");
        sb.AppendLine();
        sb.AppendLine($"namespace {ns};");
        sb.AppendLine();

        foreach (var t in deduped)
        {
            sb.AppendLine($"[NavTable(\"{t.LogicalName}\", IsShared = {t.IsShared.ToString().ToLowerInvariant()})]");
            if (t.KeyColumns.Count > 0)
                sb.AppendLine($"[NavKey({string.Join(", ", t.KeyColumns.Select(k => $"\"{k}\""))})]");
            sb.AppendLine($"public partial class {t.ModelName}");
            sb.AppendLine("{");
            var usedNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var col in t.Columns)
            {
                var baseName = col.PropertyName;
                var propName = baseName;
                for (var n = 2; !usedNames.Add(propName); n++)
                    propName = baseName + n;
                var def = col.CSharpType == "string" ? " = \"\";" : (col.CSharpType.EndsWith("?") ? "" : "");
                
                var attrBuilder = new StringBuilder();
                attrBuilder.Append($"[NavColumn(\"{col.ColumnName}\")] ");
                
                if (col.SqlType.Equals("nvarchar", StringComparison.OrdinalIgnoreCase) || col.SqlType.Equals("varchar", StringComparison.OrdinalIgnoreCase))
                {
                    // By convention in NAV, "Name", "Description", "Address" etc are Texts while IDs, Nos, Codes, User IDs are 'Code'.
                    // Simplest heuristic for generation: if column ends with 'Code', 'No', 'ID' or is exactly 'User Name', give it [NavCode]
                    var n = col.ColumnName.ToUpperInvariant();
                    if (n.EndsWith("CODE") || n.EndsWith("NO") || n.EndsWith("NO.") || n.EndsWith("ID") || n.Equals("USER_NAME") || n.Equals("USER NAME") || n.Equals("USERID"))
                    {
                        attrBuilder.Append("[NavCode] ");
                    }
                }
                
                sb.AppendLine($"    {attrBuilder}public {col.CSharpType} {propName} {{ get; set; }}{def}");
            }
            if (t.Columns.Count == 0)
                sb.AppendLine("    public string No { get; set; } = \"\";");
            sb.AppendLine("}");
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
