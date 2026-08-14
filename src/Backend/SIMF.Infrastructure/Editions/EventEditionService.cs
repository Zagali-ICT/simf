// Tests: SIMF.Api.Tests/EventEditionTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Editions.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Domain.Auditing;
using SIMF.Domain.Editions;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Editions;

/// <summary>Implements <see cref="IEventEditionService"/> over the
/// <c>EventEdition</c> singleton.</summary>
internal sealed class EventEditionService(
    SimfAppDbContext appDbContext,
    IEventEditionCache cache,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<EventEditionService> logger) : IEventEditionService
{
    /// <summary>The earliest year an admin may open. Not a style choice: the year
    /// is encoded into the badge as two bytes, so it has to fit, and a typo of
    /// "202" or "20265" must be refused before it is printed onto anything.</summary>
    private const int MinYear = 2000;
    private const int MaxYear = 2999;

    public async Task<int> GetOpenYearAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGet(out var cached)) { return cached; }
        var year = await ReadYearAsync(cancellationToken);
        cache.Set(year);
        return year;
    }

    public async Task<EventEditionState> GetAsync(CancellationToken cancellationToken = default)
    {
        var row = await LoadAsync(tracked: false, cancellationToken);
        return new EventEditionState(
            row.Year, row.OpenedAt, row.LastClosedAt, row.LastReissueCount);
    }

    public async Task<EventEditionOpenResult> OpenYearAsync(
        Guid actorUserId, int year, CancellationToken cancellationToken = default)
    {
        if (year is < MinYear or > MaxYear)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed, 400,
                $"The year must be between {MinYear} and {MaxYear}.",
                $"يجب أن تكون السنة بين {MinYear} و{MaxYear}.");
        }

        var row = await LoadAsync(tracked: true, cancellationToken);
        if (row.Year == year)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed, 409,
                "That year is already open.",
                "هذه السنة مفتوحة بالفعل.");
        }
        if (year < row.Year)
        {
            // Re-opening a closed year would make every badge issued since valid
            // again at the gate, which is the opposite of what closing it meant.
            throw new ApiException(
                ErrorCodes.ValidationFailed, 409,
                "A year earlier than the open one cannot be re-opened.",
                "لا يمكن إعادة فتح سنة أقدم من السنة المفتوحة.");
        }

        var now = timeProvider.SimfNow();
        var closingYear = row.Year;

        // Clear every badge so the new year re-issues them. Nobody is deleted:
        // the attendee records stay, labelled with the year they belong to, and
        // a returning attendee is issued a fresh badge rather than left holding
        // one that every door refuses with no route to a live one.
        //
        // A set-based UPDATE, not a load-and-loop: this is the whole attendee
        // table, and materialising it to null one column would be the slowest
        // possible way to do it at exactly the moment an operator is waiting.
        var cleared = await appDbContext.UserProfiles
            .Where(profile => profile.QrId != null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(profile => profile.QrId, (string?)null),
                cancellationToken);

        row.Year = year;
        row.OpenedAt = now;
        row.LastClosedAt = now;
        row.OpenedByUserId = actorUserId;
        row.LastReissueCount = cleared;
        await appDbContext.SaveChangesAsync(cancellationToken);
        cache.Set(year);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.EventEditionOpened,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"closed={closingYear}; opened={year}; badgesCleared={cleared}",
        }, cancellationToken);
        logger.LogInformation(
            "Admin {ActorId} closed edition {Closed} and opened {Opened}; {Cleared} badges cleared for re-issue.",
            actorUserId, closingYear, year, cleared);

        return new EventEditionOpenResult(year, cleared);
    }

    private Task<int> ReadYearAsync(CancellationToken cancellationToken) =>
        appDbContext.EventEdition
            .AsNoTracking()
            .Where(edition => edition.Id == EventEdition.SingletonId)
            .Select(edition => edition.Year)
            .SingleAsync(cancellationToken);

    private async Task<EventEdition> LoadAsync(bool tracked, CancellationToken cancellationToken)
    {
        var query = tracked
            ? appDbContext.EventEdition
            : appDbContext.EventEdition.AsNoTracking();
        return await query.SingleOrDefaultAsync(
                edition => edition.Id == EventEdition.SingletonId, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.InternalError, 500,
                "No event edition is configured.",
                "لا توجد نسخة فعالية مهيأة.");
    }
}

/// <summary>The open year, held in memory.
///
/// <para>It is read on nearly every attendee write and on every gate scan, and
/// changes about once a year, so re-reading it per scan would be a query for a
/// value that is almost never different. Invalidated by the one write that can
/// change it, so there is no TTL to be wrong about.</para></summary>
public interface IEventEditionCache
{
    bool TryGet(out int year);
    void Set(int year);
}

internal sealed class EventEditionCache : IEventEditionCache
{
    private int _year;

    public bool TryGet(out int year)
    {
        year = Volatile.Read(ref _year);
        return year != 0;
    }

    public void Set(int year) => Volatile.Write(ref _year, year);
}
