using SIMF.Common.Enums;

namespace SIMF.Domain.Auditing;

/// <summary>
/// D-109: one row of the table-level audit trail — captures every
/// INSERT / UPDATE / DELETE performed through EF on the tables that
/// are not on the exclusion list (the interceptor in
/// <c>SIMF.Infrastructure.Auditing.RowAuditingSaveChangesInterceptor</c>
/// is the single writer).
///
/// <para>Distinct from <see cref="OperationLogEntry"/>: <c>OperationLog</c>
/// captures security-relevant business events (sign-in succeeded,
/// password reset requested, admin approved). <c>RowAudit</c> captures
/// raw data changes (which column on which row changed, by which actor,
/// when, what was the before/after) — the forensic trail that answers
/// "who changed Foo.X on 2026-05-12 at 14:03?".</para>
///
/// <para>Each <see cref="SimfIdentityDbContext"/> / <see cref="SimfAppDbContext"/>
/// owns its own <c>RowAudits</c> table — keeps the same-transaction
/// guarantee (the audit row and the data row commit together).</para>
/// </summary>
public sealed class RowAudit
{
    /// <summary>Auto-increment surrogate key — ordering / paging.</summary>
    public long Id { get; set; }

    /// <summary>When the change committed (UTC).</summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>The DB table name the change happened on (e.g. <c>AspNetUsers</c>).</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>The CLR entity-type name (e.g. <c>SimfUser</c>) — for analytics
    /// that don't want to know the table mapping.</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>INSERT / UPDATE / DELETE.</summary>
    public RowAuditOperation Operation { get; set; }

    /// <summary>The row's primary-key value, JSON-encoded for composite keys.</summary>
    public string PrimaryKey { get; set; } = string.Empty;

    /// <summary>The user who performed the change; null if the change came
    /// from a background worker / seeder (no request context).</summary>
    public Guid? ActorUserId { get; set; }

    /// <summary>The request correlation id, when available.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Pre-image JSON for UPDATE and DELETE; null for INSERT.</summary>
    public string? OldValuesJson { get; set; }

    /// <summary>Post-image JSON for INSERT and UPDATE; null for DELETE.</summary>
    public string? NewValuesJson { get; set; }

    /// <summary>Comma-separated list of column names that changed
    /// (UPDATE only); null for INSERT and DELETE.</summary>
    public string? AffectedColumns { get; set; }
}
