namespace Tyresoles.Data.Features.NavisionEdits.Entities;

/// <summary>
/// Defines a request type template that admins manage from the UI.
/// The FieldsJson column stores the JSON template that drives the dynamic form
/// and controls which fields can be edited.
/// </summary>
public class NavEditRequestType
{
    public int Id { get; set; }

    /// <summary>Display name shown in dropdowns (e.g. "Customer Master", "G/L Entry", "User Setup").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Short machine key (e.g. "CUSTOMER", "GL_ENTRY", "USER_SETUP").</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Optional description for admins.</summary>
    public string? Description { get; set; }

    /// <summary>Lucide icon name for UI (e.g. "users", "book-open", "settings").</summary>
    public string? Icon { get; set; }

    /// <summary>
    /// The Nav table name to query from Db_Live (e.g. "Customer", "G_L Entry", "User Setup").
    /// Used for dynamic record lookup.
    /// </summary>
    public string NavTable { get; set; } = string.Empty;

    /// <summary>
    /// The primary key column in the Nav table (e.g. "No_", "Entry No_", "User ID").
    /// Used to fetch the specific record.
    /// </summary>
    public string NavPrimaryKeyColumn { get; set; } = string.Empty;

    /// <summary>
    /// JSON template defining the editable fields and approval workflow.
    /// Structure:
    /// {
    ///   "itAdminUserId": "OPTIONAL_NAV_USER_ID",
    ///   "fields": [
    ///     { "column": "Name", "label": "Customer Name", "type": "text" }
    ///   ],
    ///   "approvals": [
    ///     {
    ///       "userIds": ["USER1", "USER2"],
    ///       "when": [{ "column": "Blocked", "op": "eq", "value": "All" }]
    ///     }
    ///   ],
    ///   "displayColumns": ["No_", "Name", { "column": "Posting Date", "format": "date" }, { "column": "Amount", "format": "number" }]
    /// }
    /// Each approval step lists Nav user IDs who may approve that level. Optional "when" conditions (AND)
    /// filter steps using the current Nav record; steps are stored with levels 1..n after filtering.
    /// </summary>
    public string FieldsJson { get; set; } = "{}";

    /// <summary>Whether this type is active and visible to users.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Sort order in the dropdown.</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
