using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Sql.Core.Query;

internal interface IQueryInternal
{
    QueryDescriptor Build(ParameterContext context, string? parameterPrefix);
    bool HasJoins { get; }
}
