namespace SIMF.Application.Abstractions;

/// <summary>
/// Exposes the request-context fields the audit log needs, without coupling the
/// Application or Infrastructure layers to ASP.NET Core's <c>HttpContext</c>.
/// The API layer supplies the implementation.
/// </summary>
public interface IRequestContext
{
    /// <summary>The client IP the current request came from; null if none.</summary>
    string? SourceIp { get; }

    /// <summary>The client user-agent of the current request; null if none.</summary>
    string? UserAgent { get; }

    /// <summary>The correlation id of the current request; null if none.</summary>
    string? CorrelationId { get; }
}
