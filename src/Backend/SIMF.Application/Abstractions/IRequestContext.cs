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

    /// <summary>
    /// D-109: the signed-in user id from the JWT / cookie claim; null when
    /// no user is bound to the request (seeder, background worker,
    /// anonymous request). Used by the row-audit interceptor to stamp
    /// the actor on every captured change without each service having
    /// to pass the id down.
    /// </summary>
    Guid? ActorUserId { get; }
}
