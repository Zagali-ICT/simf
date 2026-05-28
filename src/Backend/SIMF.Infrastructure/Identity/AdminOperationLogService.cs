// Tests: SIMF.Api.Tests/AdminOperationLogTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// D-134 Sprint A — admin viewer over the <c>OperationLogEntry</c> table.
/// Read-only, AsNoTracking. **No schema change** — uses the existing
/// <see cref="SimfAppDbContext.OperationLog"/> DbSet.
/// </summary>
internal sealed class AdminOperationLogService(SimfAppDbContext dbContext)
    : IAdminOperationLogService
{
    public async Task<GridPage<AdminOperationLogSummary>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 25, 1, 200);

        var rows = dbContext.OperationLog.AsNoTracking().AsQueryable();

        if (query.Filters.TryGetValue("eventType", out var eventType)
            && !string.IsNullOrWhiteSpace(eventType))
        {
            var term = eventType.Trim();
            rows = rows.Where(row => EF.Functions.Like(row.EventType, $"%{term}%"));
        }
        if (query.Filters.TryGetValue("outcome", out var outcomeFilter)
            && !string.IsNullOrWhiteSpace(outcomeFilter)
            && Enum.TryParse<AuditOutcome>(outcomeFilter, ignoreCase: true, out var outcome))
        {
            rows = rows.Where(row => row.Outcome == outcome);
        }
        if (query.Filters.TryGetValue("actorUserId", out var actorRaw)
            && Guid.TryParse(actorRaw, out var actorId))
        {
            rows = rows.Where(row => row.ActorUserId == actorId);
        }
        if (query.Filters.TryGetValue("subjectEmail", out var email)
            && !string.IsNullOrWhiteSpace(email))
        {
            var term = email.Trim();
            rows = rows.Where(row =>
                row.SubjectEmail != null
                && EF.Functions.Like(row.SubjectEmail, $"%{term}%"));
        }
        if (query.Filters.TryGetValue("from", out var fromRaw)
            && DateTimeOffset.TryParse(fromRaw, out var from))
        {
            rows = rows.Where(row => row.TimestampUtc >= from);
        }
        if (query.Filters.TryGetValue("to", out var toRaw)
            && DateTimeOffset.TryParse(toRaw, out var to))
        {
            rows = rows.Where(row => row.TimestampUtc <= to);
        }

        // Default sort: newest first. Other sorts cover the column
        // headers the page exposes.
        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("eventtype", true) => rows.OrderByDescending(row => row.EventType)
                                       .ThenByDescending(row => row.TimestampUtc),
            ("eventtype", false) => rows.OrderBy(row => row.EventType)
                                        .ThenByDescending(row => row.TimestampUtc),
            ("outcome", true) => rows.OrderByDescending(row => row.Outcome)
                                     .ThenByDescending(row => row.TimestampUtc),
            ("outcome", false) => rows.OrderBy(row => row.Outcome)
                                      .ThenByDescending(row => row.TimestampUtc),
            ("timestamputc", false) => rows.OrderBy(row => row.TimestampUtc),
            _ => rows.OrderByDescending(row => row.TimestampUtc),
        };

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows
            .Skip(skip)
            .Take(top)
            .Select(row => new AdminOperationLogSummary(
                row.Id,
                row.TimestampUtc,
                row.EventType,
                row.Outcome.ToString(),
                row.SubjectEmail,
                row.ActorUserId,
                row.SourceIp,
                row.CorrelationId,
                row.ErrorCode))
            .ToListAsync(cancellationToken);

        return GridPage<AdminOperationLogSummary>.Of(page, total,
            new GridQuery { Skip = skip, Top = top });
    }

    public async Task<AdminOperationLogDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.OperationLog
            .AsNoTracking()
            .Where(row => row.Id == id)
            .Select(row => new AdminOperationLogDetail(
                row.Id,
                row.TimestampUtc,
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
