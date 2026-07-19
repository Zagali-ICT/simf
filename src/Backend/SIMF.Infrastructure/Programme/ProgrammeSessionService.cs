// Tests: SIMF.Api.Tests/ProgrammeSessionsTests.cs
// Tests: SIMF.Api.Tests/SessionLifecycleTests.cs (P3.2a — D-231 public status read)
// Tests: SIMF.Api.Tests/SessionRecordingTests.cs (P3.2b — D-232 published-recording gate)
// Tests: SIMF.Api.Tests/RecordedQuestionsTests.cs (P3.4 — D-235 recorded Q&A archive)
// Tests: SIMF.Api.Tests/SessionSummaryTests.cs (P4.1a — D-237 published-summary read)
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
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
    SimfIdentityDbContext identityDbContext,
    TimeProvider timeProvider)
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
            // A6c — half-open EVENT-LOCAL (+03:00) day window [dayStart, nextDayStart).
            // The app sends ProgrammeDay.Date (a Riyadh calendar date) as ?day=, and
            // the day-grouped agenda (ListDaysAsync) buckets by StartUtc.ToOffset(+03:00),
            // so this filter must use the SAME +03:00 boundary or the flat list would
            // disagree with the app's day strip at the UTC-midnight edge. Still a plain
            // range on StartUtc (index-friendly; no EF date-component translation).
            var dayStart = new DateTimeOffset(d.Year, d.Month, d.Day, 0, 0, 0, EventOffset);
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
                // D-452 (Figma 883:2308): the session's type for the app's tabs.
                session.Type,
                // B9b — D-226: the session's category (dynamic lookup), if set.
                session.CategoryId,
                CategoryName = session.Category != null ? session.Category.Name : null,
                CategoryNameArabic = session.Category != null ? session.Category.NameArabic : null,
                // D-252 (Mockup screen 16/17): the body + ordered speaker cards, so
                // the cached agenda payload also drives the session detail/preview
                // without a second fetch.
                session.Description,
                session.DescriptionArabic,
                // A8 — D-237: does this session have a PUBLISHED محضر? There is no
                // Session→SessionSummary navigation, so this is a correlated EXISTS
                // over SessionSummaries (the pattern AdminSessionSummaryService uses).
                // Gate matches the summary read: an active summary that is BOTH
                // published AND team-approved (owner 2026-07-19 — the app never sees an
                // unreviewed summary, so PublishedAt alone is not enough; this also
                // hides any legacy row published before the approval gate existed).
                // Session.IsActive is already ensured by the outer Where.
                HasPublishedSummary = dbContext.SessionSummaries.Any(summary =>
                    summary.SessionId == session.Id
                    && summary.IsActive
                    && summary.PublishedAt != null
                    && summary.ApprovedAt != null),
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
                        link.Speaker!.RankArabic,
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
                            speaker.PhotoRelativePath,
                            TitleArabic: speaker.RankArabic);
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
                    speakers,
                    // D-452: the session's type (Workshop / Session / Event).
                    row.Type,
                    // A8 — D-237: whether a published محضر exists for this session.
                    row.HasPublishedSummary);
            })
            .ToList();

        return new PublicSessions(items);
    }

    /// <summary>The event's local-day boundary (KSA, UTC+3). Sessions are stored
    /// as true UTC; a "programme day" is a Riyadh calendar day, so sessions are
    /// bucketed by their start in this zone (a 02:00-KSA session belongs to that
    /// KSA day, not the previous UTC day).</summary>
    private static readonly TimeSpan EventOffset = TimeSpan.FromHours(3);

    public async Task<PublicProgrammeDays> ListDaysAsync(
        CancellationToken cancellationToken = default)
    {
        // D-452 (Figma 883:2308 "تفاصيل اليوم"): the day-grouped agenda. Pull the
        // whole programme once and bucket sessions by their EVENT-LOCAL date
        // (ProgrammeDay.Date is a Riyadh calendar date), then attach each bucket
        // to its authored day. No per-day query (no N+1).
        var allSessions = (await ListAsync(null, cancellationToken)).Items;
        var byDate = allSessions
            .GroupBy(s => DateOnly.FromDateTime(s.StartUtc.ToOffset(EventOffset).DateTime))
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<PublicSessionListItem>)g.ToList());

        var days = await dbContext.ProgrammeDays
            .AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.DisplayOrder).ThenBy(d => d.Date)
            .Select(d => new { d.Id, d.Date, d.Title, d.TitleArabic, d.DisplayOrder })
            .ToListAsync(cancellationToken);

        // Fallback: no authored ProgrammeDay rows yet (the CP day-manager is a
        // later phase) → synthesize one day per distinct session date so the
        // agenda still renders the whole programme — a strict superset of the old
        // /programme/sessions screen, so a deploy never blanks the screen.
        if (days.Count == 0)
        {
            var synthesized = byDate.Keys
                .OrderBy(date => date)
                .Select((date, i) =>
                {
                    var label = date.ToString("d MMM", CultureInfo.InvariantCulture);
                    return new PublicProgrammeDay(
                        SyntheticDayId(date), date, label, label, i, false,
                        byDate[date]);
                })
                .ToList();
            return new PublicProgrammeDays(synthesized);
        }

        // Which authored days have a linked image (D-568 (S1) — StoredFile store).
        var dayIds = days.Select(d => d.Id).ToList();
        var withImage = (await dbContext.StoredFiles
                .AsNoTracking()
                .Where(f => f.IsActive
                    && f.Service == FileService.ProgrammeDayImage
                    && f.OwnerEntityId != null
                    && dayIds.Contains(f.OwnerEntityId.Value))
                .Select(f => f.OwnerEntityId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var result = days
            .Select(d => new PublicProgrammeDay(
                d.Id, d.Date, d.Title, d.TitleArabic, d.DisplayOrder,
                withImage.Contains(d.Id),
                byDate.TryGetValue(d.Date, out var sessions)
                    ? sessions
                    : Array.Empty<PublicSessionListItem>()))
            .ToList();

        return new PublicProgrammeDays(result);
    }

    /// <summary>A deterministic, per-date GUID for a synthesized (un-authored)
    /// programme day, so the day-strip selection key is stable + distinct per
    /// date (the real authored days carry their own row Id).</summary>
    private static Guid SyntheticDayId(DateOnly date) =>
        new($"{date.Year:D4}{date.Month:D2}{date.Day:D2}-0000-0000-0000-000000000000");

    public async Task<PublicSessionDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        // A6 — project the session header + its two child collections (themes,
        // speakers) as independent per-collection sub-selects, mirroring ListAsync.
        // The prior multi-Include (Hall + Speakers + Themes + Category) JOINed the
        // two SIBLING collections into one rowset, materialising a speakers×themes
        // cartesian product with the full Session/Hall/Category columns duplicated
        // on every row (EF's MultipleCollectionIncludeWarning). The projection emits
        // one APPLY per collection — no cross product, only the needed columns.
        var row = await dbContext.Sessions
            .AsNoTracking()
            .Where(session => session.Id == id && session.IsActive)
            .Select(session => new
            {
                session.Id,
                session.Code,
                session.Title,
                session.TitleArabic,
                session.Description,
                session.DescriptionArabic,
                session.HallId,
                HallName = session.Hall!.Name,
                HallNameArabic = session.Hall!.NameArabic,
                HallCapacity = (int?)session.Hall!.Capacity,
                session.StartUtc,
                session.EndUtc,
                session.CapacityOverride,
                session.Status,
                session.PublishedAt,
                HasRecordingFile = session.RecordingStoredFileName != null,
                session.LiveStreamUrl,
                session.LiveSignLanguageUrl,
                session.LiveCaptions,
                session.LiveCaptionsArabic,
                session.CategoryId,
                CategoryName = session.Category != null ? session.Category.Name : null,
                CategoryNameArabic =
                    session.Category != null ? session.Category.NameArabic : null,
                session.Language,
                session.LanguageArabic,
                Themes = session.Themes
                    .Where(link => link.Theme!.IsActive)
                    .Select(link => new
                    {
                        link.Theme!.Id,
                        link.Theme!.Name,
                        link.Theme!.NameArabic,
                        link.Theme!.PageColor,
                        link.Theme!.DisplayOrder,
                        link.Theme!.Description,
                        link.Theme!.DescriptionArabic,
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
                        link.Speaker!.RankArabic,
                        link.DisplayOrder,
                        link.Role,
                        link.Speaker!.CountryId,
                        link.Speaker!.PhotoRelativePath,
                    })
                    .ToList(),
                // Website Session-detail "أبرز المخرجات" bullets (Figma 5991-85840),
                // active + ordered.
                Outcomes = session.Outcomes
                    .Where(outcome => outcome.IsActive)
                    .OrderBy(outcome => outcome.DisplayOrder)
                    .Select(outcome => new { outcome.Text, outcome.TextArabic })
                    .ToList(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var reserved = await dbContext.SeatReservations
            .AsNoTracking()
            .CountAsync(
                r => r.SessionId == id && r.ReleasedAt == null,
                cancellationToken);

        var effectiveCapacity =
            row.CapacityOverride ?? row.HallCapacity ?? 0;

        var themes = row.Themes
            .OrderBy(theme => theme.DisplayOrder)
            .ThenBy(theme => theme.Name)
            .Select(theme => new PublicSessionTheme(
                theme.Id,
                theme.Name,
                theme.NameArabic,
                theme.PageColor,
                theme.Description,
                theme.DescriptionArabic))
            .ToList();

        // §7: resolve the country names for the detail's speakers in one query.
        var detailCountries = await ResolveCountriesAsync(
            row.Speakers
                .Where(speaker => speaker.CountryId.HasValue)
                .Select(speaker => speaker.CountryId!.Value),
            cancellationToken);

        // D-357/D-568 — which of the detail's speakers have an active photo asset
        // (one batched query; OwnerEntityId is the speaker id), so the Website
        // page can serve the portrait via the /content/assets/SpeakerPhoto proxy.
        var speakerIds = row.Speakers.Select(speaker => speaker.Id).ToList();
        var speakersWithPhoto = (await dbContext.StoredFiles
            .AsNoTracking()
            .Where(file => file.Service == FileService.SpeakerPhoto
                && file.IsActive
                && file.OwnerEntityId != null
                && speakerIds.Contains(file.OwnerEntityId.Value))
            .Select(file => file.OwnerEntityId!.Value)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var speakers = row.Speakers
            .OrderBy(speaker => speaker.DisplayOrder)
            .Select(speaker =>
            {
                string? countryEn = null, countryAr = null;
                if (speaker.CountryId is { } countryId
                    && detailCountries.TryGetValue(countryId, out var country))
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
                    speaker.PhotoRelativePath,
                    speakersWithPhoto.Contains(speaker.Id),
                    TitleArabic: speaker.RankArabic);
            })
            .ToList();

        var seats = new PublicSessionSeatSummary(
            effectiveCapacity,
            reserved,
            Math.Max(0, effectiveCapacity - reserved));

        // Website Session-detail "أبرز المخرجات" — already active + ordered above.
        var outcomes = row.Outcomes
            .Select(outcome => new PublicSessionOutcome(outcome.Text, outcome.TextArabic))
            .ToList();

        // Website Session-detail "روابط التحميل" — the session's downloadable
        // presentation files, PUBLIC per the owner decision (2026-07-15). A
        // separate query: SpeakerPresentation carries SessionId but no Session
        // back-nav. Ordered by upload time for a stable list.
        var downloads = await dbContext.SpeakerPresentations
            .AsNoTracking()
            .Where(presentation => presentation.SessionId == id && presentation.IsActive)
            .OrderBy(presentation => presentation.CreatedAt)
            .Select(presentation => new PublicSessionDownload(
                presentation.Id,
                presentation.FileName,
                presentation.ContentType,
                presentation.SizeBytes))
            .ToListAsync(cancellationToken);

        // D-567 (Figma 889:2604) — the gold badge shows the session's 1-based
        // position within its day. A6c — match the agenda's day grouping exactly:
        // a half-open EVENT-LOCAL (+03:00) window ordered by StartUtc (ProgrammeDay
        // is a Riyadh calendar day, and both ListDaysAsync and the ?day= list filter
        // bucket by the +03:00 date). Count the earlier active sessions in the same
        // event-local day; +1 is this session's ordinal.
        var localDate = row.StartUtc.ToOffset(EventOffset).Date;
        var dayStart = new DateTimeOffset(localDate, EventOffset);
        var nextDayStart = dayStart.AddDays(1);
        var displayOrder = 1 + await dbContext.Sessions
            .AsNoTracking()
            .CountAsync(
                sibling => sibling.IsActive
                    && sibling.StartUtc >= dayStart
                    && sibling.StartUtc < nextDayStart
                    && sibling.StartUtc < row.StartUtc,
                cancellationToken);

        return new PublicSessionDetail(
            row.Id,
            row.Code,
            row.Title,
            row.TitleArabic,
            row.Description,
            row.DescriptionArabic,
            row.HallId,
            row.HallName,
            row.HallNameArabic,
            row.StartUtc,
            row.EndUtc,
            themes,
            speakers,
            seats,
            row.CategoryId,
            row.CategoryName,
            row.CategoryNameArabic,
            row.Status,
            row.PublishedAt,
            // P3.2b — D-232: the app shows a player only for a published
            // session that actually has a recording.
            row.Status == SessionStatus.Published && row.HasRecordingFile,
            // §8: the live broadcast feed(s) — null when the session is not live.
            row.LiveStreamUrl,
            row.LiveSignLanguageUrl,
            // P5 — D-439: the AI live-caption text (null when none set).
            row.LiveCaptions,
            row.LiveCaptionsArabic,
            // D-567: the gold-badge ordinal (1-based position within the day).
            displayOrder,
            // Website Session-detail (Figma 5991-85840): outcomes + language label.
            outcomes,
            row.Language,
            row.LanguageArabic,
            // "روابط التحميل" — the session's public downloadable files.
            downloads);
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
        // S-6 (owner) — a محضر is viewable only once the session has actually
        // STARTED (in-progress or finished); it stays hidden before the session
        // begins. Keyed on the CLOCK (now >= StartUtc), never the manual Held flag,
        // because "logically you can't view a summary before the session starts".
        var now = timeProvider.GetUtcNow();
        return await dbContext.SessionSummaries
            .AsNoTracking()
            .Where(summary => summary.SessionId == id
                && summary.IsActive
                && summary.PublishedAt != null
                // Owner 2026-07-19 — the app only sees a summary the scientific team
                // APPROVED; this also hides any legacy row published before the approval
                // gate existed (PublishedAt set, ApprovedAt null).
                && summary.ApprovedAt != null
                && summary.Session!.IsActive
                && summary.Session!.StartUtc <= now)
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

    public async Task<HostSessionSummary?> GetApprovedSummaryForHostAsync(
        Guid callerUserId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        // Authz (D-472): only the session's moderator (the SessionModerator grant)
        // or its host (a speaker with Role=Host mapped to this user) may read the
        // approved-but-maybe-unpublished محضر — "ready for المحاور".
        var isModerator = await dbContext.SessionModerators
            .AsNoTracking()
            .AnyAsync(m => m.SessionId == sessionId && m.UserId == callerUserId, cancellationToken);

        var isHost = false;
        if (!isModerator)
        {
            var profileId = await dbContext.UserProfiles
                .AsNoTracking()
                .Where(p => p.UserId == callerUserId)
                .Select(p => (Guid?)p.Id)
                .SingleOrDefaultAsync(cancellationToken);
            if (profileId is not null)
            {
                isHost = await dbContext.SessionSpeakers
                    .AsNoTracking()
                    .AnyAsync(
                        ss => ss.SessionId == sessionId
                            && ss.Role == SessionSpeakerRole.Host
                            && ss.Speaker!.IsActive
                            && ss.Speaker!.UserProfileId == profileId,
                        cancellationToken);
            }
        }

        if (!isModerator && !isHost)
        {
            throw new ApiException(
                ErrorCodes.Forbidden, 403,
                "Only the session host or a session moderator can view the approved summary.",
                "يمكن لمحاور الجلسة أو منسّق الجلسة فقط عرض الملخّص المعتمد.");
        }

        // Gated on the team approval stamp (not the public publish): the host /
        // moderator sees the approved محضر even before a public release.
        return await dbContext.SessionSummaries
            .AsNoTracking()
            .Where(summary => summary.SessionId == sessionId
                && summary.IsActive
                && summary.ApprovedAt != null
                && summary.Session!.IsActive)
            .Select(summary => new HostSessionSummary(
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
                summary.ApprovedAt!.Value))
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
