using SIMF.Common.Enums;

namespace SIMF.Domain.Auditing;

/// <summary>
/// One entry in the operation log — the durable audit trail of
/// security-relevant events (SIMF-FDS-001 section 9). The operation log is
/// append-only.
/// </summary>
public class OperationLogEntry
{
    public Guid Id { get; set; }

    /// <summary>When the event occurred (UTC).</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>The stable event-type name (see <c>AuditEvents</c>).</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Whether the operation succeeded or failed.</summary>
    public AuditOutcome Outcome { get; set; }

    /// <summary>The email address the event concerns; null if not applicable.</summary>
    public string? SubjectEmail { get; set; }

    /// <summary>The user the event concerns; null if not applicable.</summary>
    public Guid? SubjectUserId { get; set; }

    /// <summary>D-157 — snapshot of the subject's display name at the
    /// time of the event. Lets a SOC reviewer read "Ahmad Salem" without
    /// joining back to a cross-DB Identity row that may have been
    /// renamed or deleted since. Null when the event has no subject.</summary>
    public string? SubjectDisplayName { get; set; }

    /// <summary>
    /// The user who performed the action, when distinct from the subject —
    /// e.g. an admin resetting another user's 2FA (D-041). Null in the usual
    /// case where the actor is the subject.
    /// </summary>
    public Guid? ActorUserId { get; set; }

    /// <summary>D-157 — snapshot of the actor's display name at the time
    /// of the event. Same rationale as <see cref="SubjectDisplayName"/>.
    /// Null when the actor and subject are the same (the subject's
    /// display name covers both) or when the event has no actor.</summary>
    public string? ActorDisplayName { get; set; }

    /// <summary>The client IP the request came from.</summary>
    public string? SourceIp { get; set; }

    /// <summary>The client user-agent.</summary>
    public string? UserAgent { get; set; }

    /// <summary>The request correlation id, for stitching multi-step activity.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>The API error code, when the event is a failure.</summary>
    public string? ErrorCode { get; set; }

    /// <summary>Optional extra detail; never a secret.</summary>
    public string? Detail { get; set; }
}
