// Tests: SIMF.Api.Tests/AdminOperationLogExportTests.cs,
//        SIMF.Api.Tests/GridDateSortKeyTests.cs (pages /admin/operation-log/list
//        and pins the "timestamp" sort key), SIMF.Api.Tests/GridContractTests.cs
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Auditing;
using SIMF.Application.Excel;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Grids;
using SIMF.Contracts.Admin;
using SIMF.Domain.Auditing;
using SIMF.Infrastructure.Common.Grids;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// Admin viewer over the <c>OperationLogEntry</c> table.
/// Read-only, AsNoTracking. **No schema change** — uses the existing
/// <see cref="SimfAppDbContext.OperationLog"/> DbSet. The list and the XLSX
/// export share one filter/sort path.
/// </summary>
internal sealed class AdminOperationLogService(
    SimfAppDbContext dbContext,
    IAuditLog auditLog,
    IOperationLogExcelService excel)
    : IAdminOperationLogService
{
    /// <summary>The export bound. Matches the user export's cap so an
    /// accidental "export everything" can't load the whole table into RAM;
    /// admins narrow with the filters (incl. the date range) then export.</summary>
    private const int ExportRowCap = 5_000;

    /// <summary>
    /// The grid contract for /admin/operation-log: every key OperationLogViewer can
    /// send, as its filter and as its sort. <c>actorUserId</c> is not a column on
    /// the page; it is kept because the endpoint accepted it before this list moved
    /// onto the shared grid and an API caller may still send it.
    /// </summary>
    private static readonly GridColumns<OperationLogEntry> Columns =
        new GridColumns<OperationLogEntry>()
            .Add("timestamp", row => row.Timestamp)
            .Add("eventType", row => row.EventType)
            .Add("outcome", row => row.Outcome)
            .Add("subjectEmail", row => row.SubjectEmail)
            .Add("sourceIp", row => row.SourceIp)
            .Add("actorUserId", row => row.ActorUserId)
            // The page's date range is two half-ranges over ONE column, which no
            // per-column operator can express, so both are hand-written.
            .AddFilter("from", raw =>
            {
                var start = GridFilters.ParseDay("from", raw);
                return row => row.Timestamp >= start;
            })
            // Half-open, deliberately. The bound used to be `Timestamp <= to`, and
            // the picker hands over a bare date, so asking for "up to the 16th"
            // dropped every entry after midnight on the 16th — the whole last day of
            // the range, which is usually the day the incident being traced happened.
            .AddFilter("to", raw =>
            {
                var end = GridFilters.ParseDay("to", raw).AddDays(1);
                return row => row.Timestamp < end;
            })
            .DefaultOrder("timestamp", descending: true)
            .PageSize(fallback: 25, max: 200);

    private static readonly Expression<Func<OperationLogEntry, AdminOperationLogSummary>> ToSummary =
        row => new AdminOperationLogSummary(
            row.Id,
            row.Timestamp,
            row.EventType,
            row.Outcome.ToString(),
            row.SubjectEmail,
            row.ActorUserId,
            row.SourceIp,
            row.CorrelationId,
            row.ErrorCode);

    public Task<GridPage<AdminOperationLogSummary>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default) =>
        dbContext.OperationLog.ToGridPageAsync(
            query, Columns, row => row.Id, ToSummary, cancellationToken);

    public async Task<byte[]> ExportAsync(
        Guid actorUserId, GridQuery query, CancellationToken cancellationToken = default)
    {
        // The same filters, search and order as the list, over the whole result set
        // rather than one page, bounded by the export cap. Composed through the same
        // declaration so the workbook can never drift out of parity with the grid it
        // was taken from.
        var rows = await dbContext.OperationLog
            .AsNoTracking()
            .ApplyGrid(query, Columns, row => row.Id)
            .Take(ExportRowCap)
            .Select(ToSummary)
            .ToListAsync(cancellationToken);

        var bytes = excel.Export(rows);

        await auditLog.WriteSuccessAsync(
            AuditEvents.AdminOperationLogExported,
            actorUserId,
            $"count={rows.Count}",
            cancellationToken);

        return bytes;
    }

    public async Task<AdminOperationLogDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.OperationLog
            .AsNoTracking()
            .Where(row => row.Id == id)
            .Select(row => new AdminOperationLogDetail(
                row.Id,
                row.Timestamp,
                row.EventType,
                row.Outcome.ToString(),
                row.SubjectEmail,
                row.SubjectUserId,
                row.ActorUserId,
                row.SourceIp,
                row.UserAgent,
                row.CorrelationId,
                row.ErrorCode,
                row.Detail))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
