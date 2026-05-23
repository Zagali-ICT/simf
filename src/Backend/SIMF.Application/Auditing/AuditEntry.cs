using SIMF.Domain.Auditing;

namespace SIMF.Application.Auditing;

/// <summary>
/// The business fields of an audit event. The request-context fields — source
/// IP, user-agent, correlation id — are added by the audit-log implementation.
/// </summary>
public sealed record AuditEntry
{
    public required string EventType { get; init; }

    public required AuditOutcome Outcome { get; init; }

    public string? SubjectEmail { get; init; }

    public Guid? SubjectUserId { get; init; }

    /// <summary>
    /// The user who *performed* the action, when the actor is different from
    /// the subject — for example, an admin resetting another user's 2FA (D-041).
    /// Null when actor and subject are the same person (the usual case).
    /// </summary>
    public Guid? ActorUserId { get; init; }

    public string? ErrorCode { get; init; }

    public string? Detail { get; init; }
}
