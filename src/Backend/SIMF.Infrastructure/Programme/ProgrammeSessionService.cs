// Tests: SIMF.Api.Tests/ProgrammeSessionsTests.cs
// Tests: SIMF.Api.Tests/SessionLifecycleTests.cs (P3.2a — D-231 public status read)
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Programme.Abstractions;
using SIMF.Contracts.Programme;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Programme;

/// <summary>
/// D-199 (gap doc G3, Mockup pages 16-17) — public, anonymous reads over
/// the programme <see cref="SIMF.Domain.Programme.Session"/> surface.
/// Read-only sibling of <see cref="AdminSessionService"/>: only active
/// sessions are returned (<c>IsActive</c>), times stay UTC, and the
/// effective capacity is <c>CapacityOverride ?? Hall.Capacity</c>
/// (PDF §2.9). Seat availability is a single COUNT over active
/// (non-released) reservations — no per-seat grid (that is the
/// seat-map endpoint's job).
/// </summary>
internal sealed class ProgrammeSessionService(SimfAppDbContext dbContext)
    : IProgrammeSessionService
{
    public async Task<PublicSessions> ListAsync(
        DateOnly? day, CancellationToken cancellationToken = default)
    {
        var rows = dbContext.Sessions
            .AsNoTracking()
            .Where(session => session.IsActive)
            .AsQueryable();

        if (day is { } d)
        {
            // Half-open UTC range [dayStart, nextDayStart) — index-friendly
            // and avoids relying on EF date-component translation.
            var dayStart = new DateTimeOffset(d.Year, d.Month, d.Day, 0, 0, 0, TimeSpan.Zero);
            var nextDayStart = dayStart.AddDays(1);
            rows = rows.Where(session =>
                session.StartUtc >= dayStart && session.StartUtc < nextDayStart);
        }

        // Project the session header + its active themes (one APPLY), then
        // pick the primary pillar in memory. Avoids three correlated
        // sub-selects and keeps the EF translation simple.
        var projected = await rows
            .OrderBy(session => session.StartUtc)
            .ThenBy(session => session.Title)
            .Select(session => new
            {
                session.Id,
                session.Code,
                session.Title,
                session.TitleArabic,
                session.HallId,
                HallName = session.Hall!.Name,
                HallNameArabic = session.Hall!.NameArabic,
                session.StartUtc,
                session.EndUtc,
                // P3.2 — D-231: broadcast lifecycle status.
                session.Status,
                // B9b — D-226: the session's category (dynamic lookup), if set.
                session.CategoryId,
                CategoryName = session.Category != null ? session.Category.NameEn : null,
                CategoryNameArabic = session.Category != null ? session.Category.NameAr : null,
                Themes = session.Themes
                    .Where(link => link.Theme!.IsActive)
                    .Select(link => new
                    {
                        link.Theme!.Name,
                        link.Theme!.NameArabic,
                        link.Theme!.PageColor,
                        link.Theme!.DisplayOrder,
                    })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        var items = projected
            .Select(row =>
            {
                // Primary theme = active theme with the lowest DisplayOrder.
                var primary = row.Themes
                    .OrderBy(theme => theme.DisplayOrder)
                    .ThenBy(theme => theme.Name)
                    .FirstOrDefault();
                return new PublicSessionListItem(
                    row.Id,
                    row.Code,
                    row.Title,
                    row.TitleArabic,
                    row.HallId,
                    row.HallName,
                    row.HallNameArabic,
                    row.StartUtc,
                    row.EndUtc,
                    primary?.Name,
                    primary?.NameArabic,
                    primary?.PageColor,
                    row.CategoryId,
                    row.CategoryName,
                    row.CategoryNameArabic,
                    row.Status);
            })
            .ToList();

        return new PublicSessions(items);
    }

    public async Task<PublicSessionDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var session = await dbContext.Sessions
            .AsNoTracking()
            .Include(row => row.Hall)
            .Include(row => row.Speakers).ThenInclude(link => link.Speaker)
            .Include(row => row.Themes).ThenInclude(link => link.Theme)
            .Include(row => row.Category)
            .SingleOrDefaultAsync(
                row => row.Id == id && row.IsActive, cancellationToken);

        if (session is null)
        {
            return null;
        }

        var reserved = await dbContext.SeatReservations
            .AsNoTracking()
            .CountAsync(
                r => r.SessionId == id && r.ReleasedAt == null,
                cancellationToken);

        var effectiveCapacity =
            session.CapacityOverride ?? session.Hall?.Capacity ?? 0;

        var themes = session.Themes
            .Where(link => link.Theme is not null && link.Theme.IsActive)
            .OrderBy(link => link.Theme!.DisplayOrder)
            .ThenBy(link => link.Theme!.Name)
            .Select(link => new PublicSessionTheme(
                link.Theme!.Id,
                link.Theme!.Name,
                link.Theme!.NameArabic,
                link.Theme!.PageColor))
            .ToList();

        var speakers = session.Speakers
            .Where(link => link.Speaker is not null && link.Speaker.IsActive)
            .OrderBy(link => link.DisplayOrder)
            .Select(link => new PublicSessionSpeaker(
                link.Speaker!.Id,
                link.Speaker!.Name,
                link.Speaker!.NameArabic,
                link.Speaker!.Rank,
                link.DisplayOrder,
                link.Role))
            .ToList();

        var seats = new PublicSessionSeatSummary(
            effectiveCapacity,
            reserved,
            Math.Max(0, effectiveCapacity - reserved));

        return new PublicSessionDetail(
            session.Id,
            session.Code,
            session.Title,
            session.TitleArabic,
            session.Description,
            session.DescriptionArabic,
            session.HallId,
            session.Hall?.Name ?? string.Empty,
            session.Hall?.NameArabic ?? string.Empty,
            session.StartUtc,
            session.EndUtc,
            themes,
            speakers,
            seats,
            session.CategoryId,
            session.Category?.NameEn,
            session.Category?.NameAr,
            session.Status,
            session.PublishedAt);
    }
}
