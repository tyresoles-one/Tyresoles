
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Tyresoles.Logger.Core;

public class ScopeNode
{
    public object State { get; }
    public ScopeNode? Parent { get; }
    public ScopeNode(object state, ScopeNode? parent)
    {
        State = state;
        Parent = parent;
    }
}

public readonly record struct LogEntry
(
    DateTime Timestamp,
    LogLevel Level,
    string Category,
    EventId EventId,
    string Message,
    Exception? Exception,
    string Method,
    [property: JsonIgnore] ScopeNode? Scope
)
{
    public IEnumerable<object> Scopes
    {
        get
        {
            var current = Scope;
            var stack = new Stack<object>();
            while (current != null)
            {
                stack.Push(current.State);
                current = current.Parent;
            }
            return stack;
        }
    }
}
