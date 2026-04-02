namespace Tyresoles.Data;

public static class StringExtensions
{
    public static bool IsEmpty(this string? value) => string.IsNullOrWhiteSpace(value);
    
    public static bool HasValue(this string? value) => !string.IsNullOrWhiteSpace(value);
    
    public static bool EqualsIgnoreCase(this string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
