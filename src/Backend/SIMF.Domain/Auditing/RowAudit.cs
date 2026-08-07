using SIMF.Common.Enums;

namespace SIMF.Domain.Auditing;

/// <summary>
/// One row of the table-level audit trail, capturing every insert, update and
/// delete performed through EF on the tables that are not excluded. The
/// row-auditing save-changes interceptor is its only writer.
///
/// <para>Distinct from <see cref="OperationLogEntry"/>, which records
/// security-relevant business events such as a sign-in or an approval. This
/// records raw data changes — which column on which row changed, by whom, when,
/// and what it held before and after — the forensic trail that answers "who
/// changed this field, and when?".</para>
///
/// <para>Each database owns its own audit table, which is what keeps the audit
/// row and the data row committing in the same transaction.</para>
/// </summary>
public sealed class RowAudit
{
    /// <summary>Auto-increment surrogate key, used for ordering and paging.</summary>
    public long Id { get; set; }

    /// <summary>When the change committed, in Saudi local time.</summary>
    public DateTime OccurredAt { get; set; }

    public string TableName { get; set; } = string.Empty;

    /// <summary>The CLR entity name, for analytics that would rather not know
    /// the table mapping.</summary>
    public string EntityType { get; set; } = string.Empty;

    public RowAuditOperation Operation { get; set; }

    /// <summary>The row's primary key, JSON-encoded when it is composite.</summary>
    public string PrimaryKey { get; set; } = string.Empty;

    /// <summary>Null when the change came from a background worker or the
    /// seeder, which have no request context.</summary>
    public Guid? ActorUserId { get; set; }

    /// <summary>The actor's display name at the time of the change, so a
    /// reviewer can read it without joining back to an Identity row in the other
    /// database that may since have been renamed or deleted. Null when the change
    /// had no actor.</summary>
    public string? ActorDisplayName { get; set; }

    public string? CorrelationId { get; set; }

    /// <summary>Set on update and delete; null on insert.</summary>
    public string? OldValuesJson { get; set; }

    /// <summary>Set on insert and update; null on delete.</summary>
    public string? NewValuesJson { get; set; }

    /// <summary>The columns that changed, comma-separated. Update only.</summary>
    public string? AffectedColumns { get; set; }
}
