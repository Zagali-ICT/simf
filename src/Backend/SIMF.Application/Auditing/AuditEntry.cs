using SIMF.Common.Enums;
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

    /// <summary>D-157 — optional snapshot of the subject's display name
    /// at the time of the event. Callers that already have the value
    /// should pass it; the audit log persists it so a SOC reviewer
    /// doesn't need a cross-DB JOIN. Null when the caller doesn't have
    /// the display name on hand.</summary>
    public string? SubjectDisplayName { get; init; }

    /// <summary>
    /// The user who *performed* the action, when the actor is different from
    /// the subject — for example, an admin resetting another user's 2FA (D-041).
    /// Null when actor and subject are the same person (the usual case).
    /// </summary>
    public Guid? ActorUserId { get; init; }

    /// <summary>D-157 — optional snapshot of the actor's display name.
    /// When null, the audit log falls back to the JWT's
    /// <c>display_name</c> claim via <see cref="SIMF.Application.Abstractions.IRequestContext.ActorDisplayName"/>.</summary>
    public string? ActorDisplayName { get; init; }

    public string? ErrorCode { get; init; }

    public string? Detail { get; init; }
}
