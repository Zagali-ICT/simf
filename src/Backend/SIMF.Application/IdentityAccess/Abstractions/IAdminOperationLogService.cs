using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// D-134 Sprint A — read-only viewer over the existing <c>OperationLogEntry</c>
/// table. The page reads only; writes are owned by the existing
/// <c>IAuditLog</c> abstraction.
///
/// <para>Path 2 — no schema change. Existing indexes
/// (<c>IX_OperationLog_EventType_TimestampUtc</c>) carry the typical
/// "filter by event + sort by time desc" query.</para>
/// </summary>
public interface IAdminOperationLogService
{
    /// <summary>One page of the admin grid. Filters accepted via
    /// <see cref="GridQuery.Filters"/>: <c>eventType</c>, <c>outcome</c>
    /// (Success|Failure), <c>actorUserId</c>, <c>subjectEmail</c>, <c>from</c>
    /// + <c>to</c> (ISO-8601). Defaults to newest-first.</summary>
    Task<GridPage<AdminOperationLogSummary>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    /// <summary>One row by id, or <c>null</c> when missing.</summary>
    Task<AdminOperationLogDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default);

    /// <summary>P1.6 — XLSX of the filtered result set (same
    /// <see cref="GridQuery.Filters"/> as <see cref="ListAsync"/>, bounded to a
    /// safe row cap). Writes an <c>Admin.OperationLogExported</c> audit row.</summary>
    Task<byte[]> ExportAsync(
        Guid actorUserId, GridQuery query, CancellationToken cancellationToken = default);
}
