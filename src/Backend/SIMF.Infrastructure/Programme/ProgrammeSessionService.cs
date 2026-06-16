// Tests: SIMF.Api.Tests/ProgrammeSessionsTests.cs
// Tests: SIMF.Api.Tests/SessionLifecycleTests.cs (P3.2a — D-231 public status read)
// Tests: SIMF.Api.Tests/SessionRecordingTests.cs (P3.2b — D-232 published-recording gate)
// Tests: SIMF.Api.Tests/RecordedQuestionsTests.cs (P3.4 — D-235 recorded Q&A archive)
// Tests: SIMF.Api.Tests/SessionSummaryTests.cs (P4.1a — D-237 published-summary read)
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common.Enums;
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
internal sealed class ProgrammeSessionService(
    SimfAppDbContext dbContext,
    SimfIdentityDbContext identityDbContext)
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
                CategoryName = session.Category != null ? session.Category.Name : null,
                CategoryNameArabic = session.Category != null ? session.Category.NameArabic : null,
                // D-252 (Mockup screen 16/17): the body + ordered speaker cards, so
                // the cached agenda payload also drives the session detail/preview
                // without a second fetch.
                session.Description,
                session.DescriptionArabic,
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
                Speakers = session.Speakers
                    .Where(link => link.Speaker!.IsActive)
                    .Select(link => new
                    {
                        link.Speaker!.Id,
                        link.Speaker!.Name,
                        link.Speaker!.NameArabic,
                        link.Speaker!.Rank,
                        link.DisplayOrder,
                        link.Role,
                        // §7: country (flag) + photo shown with the speaker.
                        link.Speaker!.CountryId,
                        link.Speaker!.PhotoRelativePath,
                    })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        // §7: resolve the country names for every speaker on the page in one
        // query (Speaker has a CountryId FK but no Country nav — same approach
        // as PublicSpeakerService).
        var countriesById = await ResolveCountriesAsync(
            projected.SelectMany(row => row.Speakers)
                .Where(speaker => speaker.CountryId.HasValue)
                .Select(speaker => speaker.CountryId!.Value),
            cancellationToken);

        var items = projected
            .Select(row =>
            {
                // Primary theme = active theme with the lowest DisplayOrder.
                var primary = row.Themes
                    .OrderBy(theme => theme.DisplayOrder)
                    .ThenBy(theme => theme.Name)
                    .FirstOrDefault();
                var speakers = row.Speakers
                    .OrderBy(speaker => speaker.DisplayOrder)
                    .Select(speaker =>
                    {
                        string? countryEn = null, countryAr = null;
                        if (speaker.CountryId.HasValue
                            && countriesById.TryGetValue(speaker.CountryId.Value, out var country))
                        {
                            countryEn = country.Name;
                            countryAr = country.NameArabic;
                        }
                        return new PublicSessionSpeaker(
                            speaker.Id,
                            speaker.Name,
                            speaker.NameArabic,
                            speaker.Rank,
                            speaker.DisplayOrder,
                            speaker.Role,
                            speaker.CountryId,
                            countryEn,
                            countryAr,
                            speaker.PhotoRelativePath);
                    })
                    .ToList();
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
                    row.Status,
                    row.Description,
                    row.DescriptionArabic,
                    speakers);
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

        // §7: resolve the country names for the detail's speakers in one query.
        var detailCountries = await ResolveCountriesAsync(
            session.Speakers
                .Where(link => link.Speaker is not null && link.Speaker.IsActive
                    && link.Speaker.CountryId.HasValue)
                .Select(link => link.Speaker!.CountryId!.Value),
            cancellationToken);

        var speakers = session.Speakers
            .Where(link => link.Speaker is not null && link.Speaker.IsActive)
            .OrderBy(link => link.DisplayOrder)
            .Select(link =>
            {
                string? countryEn = null, countryAr = null;
                if (link.Speaker!.CountryId is { } countryId
                    && detailCountries.TryGetValue(countryId, out var country))
                {
                    countryEn = country.Name;
                    countryAr = country.NameArabic;
                }
                return new PublicSessionSpeaker(
                    link.Speaker!.Id,
                    link.Speaker!.Name,
                    link.Speaker!.NameArabic,
                    link.Speaker!.Rank,
                    link.DisplayOrder,
                    link.Role,
                    link.Speaker!.CountryId,
                    countryEn,
                    countryAr,
                    link.Speaker!.PhotoRelativePath);
            })
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
            session.Category?.Name,
            session.Category?.NameArabic,
            session.Status,
            session.PublishedAt,
            // P3.2b — D-232: the app shows a player only for a published
            // session that actually has a recording.
            session.Status == SessionStatus.Published
                && session.RecordingStoredFileName is not null,
            // §8: the live broadcast feed(s) — null when the session is not live.
            session.LiveStreamUrl,
            session.LiveSignLanguageUrl,
            // P5 — D-439: the AI live-caption text (null when none set).
            session.LiveCaptions,
            session.LiveCaptionsArabic);
    }

    public async Task<SessionRecordingRef?> GetPublishedRecordingAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        // The public visibility gate: a recording is reachable only when the
        // session is active, Published, and a recording file is attached.
        var row = await dbContext.Sessions
            .AsNoTracking()
            .Where(session => session.Id == id
                && session.IsActive
                && session.Status == SessionStatus.Published
                && session.RecordingStoredFileName != null)
            .Select(session => new
            {
                session.RecordingStoredFileName,
                session.RecordingContentType,
                session.RecordingFileName,
            })
            .SingleOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new SessionRecordingRef(
                row.RecordingStoredFileName!,
                row.RecordingContentType ?? "application/octet-stream",
                row.RecordingFileName ?? "recording");
    }

    public async Task<IReadOnlyList<PublicRecordedQuestion>> ListRecordedQuestionsAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        // Gated like the recording: only an active, published session exposes
        // its recorded Q&A archive.
        var published = await dbContext.Sessions
            .AsNoTracking()
            .AnyAsync(
                s => s.Id == id && s.IsActive && s.Status == SessionStatus.Published,
                cancellationToken);
        if (!published)
        {
            return Array.Empty<PublicRecordedQuestion>();
        }

        var rows = await dbContext.SessionQuestions
            .AsNoTracking()
            .Where(q => q.SessionId == id && q.Status == QuestionStatus.Approved)
            .OrderBy(q => q.Order).ThenBy(q => q.CreatedAt)
            .Select(q => new
            {
                q.Id,
                q.SubmittedByUserId,
                q.QuestionText,
                q.Recipient,
                q.IsPushed,
                q.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return Array.Empty<PublicRecordedQuestion>();
        }

        // Attribute to the asker via the Identity DB (no cross-DB JOIN, D-157).
        var userIds = rows.Select(r => r.SubmittedByUserId).Distinct().ToList();
        var users = await identityDbContext.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        return rows.Select(r =>
        {
            users.TryGetValue(r.SubmittedByUserId, out var user);
            return new PublicRecordedQuestion(
                r.Id,
                r.QuestionText,
                user?.DisplayName ?? string.Empty,
                r.Recipient,
                r.IsPushed,
                r.CreatedAt);
        }).ToList();
    }

    public async Task<PublicSessionSummary?> GetSessionSummaryAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        // Gated on the summary's own publish stamp (the Committee's editorial
        // action), not the broadcast Session.Status. The session must still be
        // active (soft-delete hides its summary too).
        return await dbContext.SessionSummaries
            .AsNoTracking()
            .Where(summary => summary.SessionId == id
                && summary.IsActive
                && summary.PublishedAt != null
                && summary.Session!.IsActive)
            .Select(summary => new PublicSessionSummary(
                summary.SessionId,
                summary.KeyPoints,
                summary.KeyPointsArabic,
                summary.Recommendations,
                summary.RecommendationsArabic,
                summary.Speakers,
                summary.SpeakersArabic,
                summary.FullText,
                summary.FullTextArabic,
                summary.AiModel != null,
                summary.PublishedAt!.Value))
            .SingleOrDefaultAsync(cancellationToken);
    }

    // §7: batch-resolve country (id -> EN/AR names) for the session speakers.
    // Speaker carries a CountryId FK but no Country navigation, so the
    // projection cannot dot through to the country in SQL (mirrors
    // PublicSpeakerService.ResolveCountriesAsync).
    private async Task<IReadOnlyDictionary<int, (string Name, string NameArabic)>>
        ResolveCountriesAsync(
            IEnumerable<int> countryIds, CancellationToken cancellationToken)
    {
        var ids = countryIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, (string, string)>();
        }
        return await dbContext.Countries
            .AsNoTracking()
            .Where(country => ids.Contains(country.Id))
            .Select(country => new { country.Id, country.Name, country.NameArabic })
            .ToDictionaryAsync(
                country => country.Id,
                country => (country.Name, country.NameArabic),
                cancellationToken);
    }
}
