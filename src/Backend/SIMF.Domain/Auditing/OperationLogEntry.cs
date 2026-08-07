using SIMF.Common.Enums;

namespace SIMF.Domain.Auditing;

/// <summary>
/// One entry in the operation log, the append-only audit trail of
/// security-relevant events.
/// </summary>
public class OperationLogEntry
{
    public Guid Id { get; set; }

    /// <summary>When the event occurred, in Saudi local time.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>A stable event-type name, drawn from the audit-event
    /// constants.</summary>
    public string EventType { get; set; } = string.Empty;

    public AuditOutcome Outcome { get; set; }

    /// <summary>The email the event concerns, when it concerns one.</summary>
    public string? SubjectEmail { get; set; }

    /// <summary>The user the event concerns, when it concerns one.</summary>
    public Guid? SubjectUserId { get; set; }

    /// <summary>The subject's display name as it stood at the time. Snapshotting
    /// it keeps the log readable without joining back to an Identity row in the
    /// other database that may since have been renamed or deleted.</summary>
    public string? SubjectDisplayName { get; set; }

    /// <summary>Who performed the action, when that is someone other than the
    /// subject — an admin resetting another user's second factor, for instance.
    /// Null in the ordinary case where the actor is the subject.</summary>
    public Guid? ActorUserId { get; set; }

    /// <summary>The actor's display name at the time, held for the same reason
    /// as <see cref="SubjectDisplayName"/>. Null when actor and subject are the
    /// same person, since the subject's name covers both.</summary>
    public string? ActorDisplayName { get; set; }

    public string? SourceIp { get; set; }
    public string? UserAgent { get; set; }

    /// <summary>Stitches multi-step activity together across entries.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Set when the event is a failure.</summary>
    public string? ErrorCode { get; set; }

    /// <summary>Extra detail, never a secret.</summary>
    public string? Detail { get; set; }
}
