namespace Tyresoles.Data.Features.NavisionEdits;

/// <summary>
/// Simple key-value pair for serializing dynamic dictionary data via GraphQL.
/// Used for record lookup results where columns are dynamic.
/// </summary>
public class KeyValueItem
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
}
