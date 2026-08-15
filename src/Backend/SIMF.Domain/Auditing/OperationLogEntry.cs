using SIMF.Common.Enums;

namespace SIMF.Domain.Auditing;

/// <summary>One entry in the append-only audit trail of security-relevant events.</summary>
public class OperationLogEntry
{
    public Guid Id { get; set; }

    /// <summary>Saudi local time.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>A stable event-type name, drawn from the audit-event constants.</summary>
    public string EventType { get; set; } = string.Empty;

    public AuditOutcome Outcome { get; set; }

    public string? SubjectEmail { get; set; }
    public Guid? SubjectUserId { get; set; }

    /// <summary>Display names are snapshotted so the log reads without joining an Identity row in the other database.</summary>
    public string? SubjectDisplayName { get; set; }

    /// <summary>Set only when the actor is someone other than the subject.</summary>
    public Guid? ActorUserId { get; set; }

    public string? ActorDisplayName { get; set; }

    public string? SourceIp { get; set; }
    public string? UserAgent { get; set; }
    public string? CorrelationId { get; set; }
    public string? ErrorCode { get; set; }

    /// <summary>Extra detail, never a secret.</summary>
    public string? Detail { get; set; }
}
