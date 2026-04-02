using System.Linq;

namespace Tyresoles.Protean;

/// <summary>Base exception for all Protean GSP errors.</summary>
public class ProteanException : Exception
{
    public ProteanException(string message) : base(message) { }
    public ProteanException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Thrown when the IRP returns error code 2150 (duplicate IRN).
/// The caller can read <see cref="IrnInfo"/> to obtain the existing IRN details.
/// </summary>
public sealed class DuplicateIrnException : ProteanException
{
    public DuplicateIrnInfo IrnInfo { get; }
    public DuplicateIrnException(string message, DuplicateIrnInfo info)
        : base(message) => IrnInfo = info;
}

/// <summary>IRN detail returned inside error 2150.</summary>
public sealed record DuplicateIrnInfo(string? AckNo, string? AckDt, string? Irn);

/// <summary>
/// Thrown when the EWaybill API returns validation error codes.
/// The dictionary maps error code → human-readable message.
/// </summary>
public sealed class EWaybillGenerateException : ProteanException
{
    public IReadOnlyDictionary<string, string> Errors { get; }
    public EWaybillGenerateException(string message, Dictionary<string, string> errors)
        : base(message) => Errors = errors;

    /// <summary>
    /// Includes decoded Protean error codes and mapped messages so generic <c>catch</c> / Serilog
    /// show the same detail as <see cref="Errors"/> (base constructor message alone did not).
    /// </summary>
    public override string Message =>
        Errors.Count == 0
            ? base.Message
            : $"{base.Message} — {string.Join("; ", Errors.Select(kv => $"{kv.Key}: {kv.Value}"))}";
}
