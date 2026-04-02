using System.Collections;

namespace Tyresoles.Sql.Abstractions;

public record JoinQuery<T, U>(T Left, U Right);

public interface IGroupResult<out TKey, out TElement> : IEnumerable<TElement>
{
    TKey Key { get; }
}

public class QueryDescriptor
{
    public string Sql { get; init; } = string.Empty;
    public Dictionary<string, object> Parameters { get; init; } = new();
}

public class PagedResult<T>
{
    public T[] Items { get; init; } = Array.Empty<T>();
    public string? NextCursor { get; init; }
    public bool HasNextPage { get; init; }
}
