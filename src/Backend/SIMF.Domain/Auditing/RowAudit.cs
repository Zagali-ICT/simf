using SIMF.Common.Enums;

namespace SIMF.Domain.Auditing;

/// <summary>
/// Forensic row-level trail of every EF insert, update and delete on the tables that
/// are not excluded; the row-auditing save-changes interceptor is its only writer.
/// Each database owns its own audit table, so the audit row and the data row commit in
/// the same transaction.
/// </summary>
public sealed class RowAudit
{
    public long Id { get; set; }

    /// <summary>Saudi local time.</summary>
    public DateTime OccurredAt { get; set; }

    public string TableName { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;

    public RowAuditOperation Operation { get; set; }

    /// <summary>JSON-encoded when the key is composite.</summary>
    public string PrimaryKey { get; set; } = string.Empty;

    /// <summary>Null for background-worker and seeder writes, which have no request context.</summary>
    public Guid? ActorUserId { get; set; }

    /// <summary>Snapshotted so the trail reads without joining an Identity row in the other database.</summary>
    public string? ActorDisplayName { get; set; }

    public string? CorrelationId { get; set; }
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }

    /// <summary>Comma-separated; update only.</summary>
    public string? AffectedColumns { get; set; }
}
