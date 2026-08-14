// Tests: SIMF.Api.Tests/ProgrammeSessionsTests.cs
// Tests: SIMF.Api.Tests/SessionLifecycleTests.cs
// Tests: SIMF.Api.Tests/SessionRecordingTests.cs
// Tests: SIMF.Api.Tests/RecordedQuestionsTests.cs
// Tests: SIMF.Api.Tests/SessionSummaryTests.cs
// Tests: SIMF.Api.Tests/SessionLiveNoticeTests.cs (informational live notice)
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Options;
using SIMF.Contracts.Programme;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Programme;

/// <summary>
/// Public, anonymous reads over
/// the programme <see cref="SIMF.Domain.Programme.Session"/> surface.
/// Read-only sibling of <see cref="AdminSessionService"/>: only active
/// sessions are returned (<c>IsActive</c>), times are the Saudi wall clock
/// (nothing zoned is stored or served), and the
/// effective capacity is <c>CapacityOverride ?? Hall.Capacity</c>.
/// Seat availability is a single COUNT over active
/// (non-released) reservations — no per-seat grid (that is the
/// seat-map endpoint's job).
/// </summary>
internal sealed class ProgrammeSessionService(
    SimfAppDbContext dbContext,
    SimfIdentityDbContext identityDbContext,
    TimeProvider timeProvider,
    // The last link in the arrival-grace chain, so the app can be
    // told what the door will do instead of assuming the historical 15.
    IOptionsMonitor<WalkInModeOptions> walkInMode)
    : IProgrammeSessionService
{
    public async Task<PublicSessions> ListAsync(
        DateOnly? day, Guid? categoryId = null, CancellationToken cancellationToken = default)
    {
        var rows = dbContext.Sessions
            .AsNoTracking()
            .Where(session => session.IsActive)
            .AsQueryable();

        // Optional server-side track filter on the dynamic SessionCategory
        // lookup. A plain equality on the indexed FK; combines with ?day=
        // (AND). An unknown id simply matches nothing — no 404, because the public
        // agenda must not become a category-id oracle.
        if (categoryId is { } category)
        {
            rows = rows.Where(session => session.CategoryId == category);
        }

        if (day is { } d)
        {
            // Half-open EVENT-LOCAL (+03:00) day window [dayStart, nextDayStart).
            // The app sends ProgrammeDay.Date (a Riyadh calendar date) as ?day=, and
            // the day-grouped agenda (ListDaysAsync) buckets by Start,
            // so this filter must use the SAME day boundary or the flat list would
            // disagree with the app's day strip at the midnight edge. Still a plain
            // range on Start (index-friendly; no EF date-component translation).
            var dayStart = new DateTime(d.Year, d.Month, d.Day, 0, 0, 0);
            var nextDayStart = dayStart.AddDays(1);
            rows = rows.Where(session =>
                session.Start >= dayStart && session.Start < nextDayStart);
        }

        // Project the session header + its active themes (one APPLY), then
        // pick the primary pillar in memory. Avoids three correlated
        // sub-selects and keeps the EF translation simple.
        var projected = await rows
            .OrderBy(session => session.Start)
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
                session.Start,
                session.End,
                // Broadcast lifecycle status.
                session.Status,
                // The session's type for the app's tabs.
                session.Type,
                // The session's category (dynamic lookup), if set.
                session.CategoryId,
                CategoryName = session.Category != null ? session.Category.Name : null,
                CategoryNameArabic = session.Category != null ? session.Category.NameArabic : null,
                // The body + ordered speaker cards, so
                // the cached agenda payload also drives the session detail/preview
                // without a second fetch.
                session.Description,
                session.DescriptionArabic,
                // Does this session have a PUBLISHED محضر? There is no
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
                            null, // photo comes from the StoredFile store
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
                    row.Start,
                    row.End,
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
                    // The session's type (Workshop / Session / Event).
                    row.Type,
                    // Whether a published محضر exists for this session.
                    row.HasPublishedSummary);
            })
            .ToList();

        return new PublicSessions(items);
    }

    /// <summary>The event's local-day offset (KSA, +03:00, no DST). Sessions
    /// are stored as the Saudi wall clock, so bucketing a session into
    /// its "programme day" is a plain date comparison with no zone shift. The
    /// constant is retained because the day boundary is still a Riyadh calendar
    /// day and callers reason in that offset.</summary>
    private static readonly TimeSpan EventOffset = TimeSpan.FromHours(3);

    public async Task<PublicProgrammeDays> ListDaysAsync(
        CancellationToken cancellationToken = default)
    {
        // The day-grouped agenda ("تفاصيل اليوم"). Pull the
        // whole programme once and bucket sessions by their EVENT-LOCAL date
        // (ProgrammeDay.Date is a Riyadh calendar date), then attach each bucket
        // to its authored day. No per-day query (no N+1).
        var allSessions = (await ListAsync(null, null, cancellationToken)).Items;
        var byDate = allSessions
            .GroupBy(s => DateOnly.FromDateTime(s.Start))
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

        // Which authored days have a linked image in the StoredFile store.
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
                session.Start,
                session.End,
                session.CapacityOverride,
                session.Status,
                session.PublishedAt,
                HasRecordingFile = session.RecordingFileId != null,
                session.LiveStreamUrl,
                session.LiveSignLanguageUrl,
                session.LiveCaptions,
                session.LiveCaptionsArabic,
                // The informational live notice shown WITH the feed. Read
                // only; it takes part in no filter and gates nothing.
                session.LiveNotice,
                session.LiveNoticeArabic,
                // The two layers that can widen this session's door.
                session.ArrivalGraceMinutesOverride,
                HallArrivalGraceMinutes = session.Hall!.ArrivalGraceMinutes,
                session.CategoryId,
                CategoryName = session.Category != null ? session.Category.Name : null,
                CategoryNameArabic =
                    session.Category != null ? session.Category.NameArabic : null,
                // The session kind. The agenda list has long projected this;
                // the detail read did not, which is why a type-conditional
                // render on the detail screen could never fire.
                session.Type,
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
                    })
                    .ToList(),
                // Website Session-detail "أبرز المخرجات" bullets,
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

        // Which of the detail's speakers have an active photo asset
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
                    null, // photo comes from the StoredFile store
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

        // The gold badge shows the session's 1-based
        // position within its day. Match the agenda's day grouping exactly:
        // a half-open EVENT-LOCAL (+03:00) window ordered by Start (ProgrammeDay
        // is a Riyadh calendar day, and both ListDaysAsync and the ?day= list filter
        // bucket by the +03:00 date). Count the earlier active sessions in the same
        // event-local day; +1 is this session's ordinal.
        var dayStart = row.Start.Date;
        var nextDayStart = dayStart.AddDays(1);
        var displayOrder = 1 + await dbContext.Sessions
            .AsNoTracking()
            .CountAsync(
                sibling => sibling.IsActive
                    && sibling.Start >= dayStart
                    && sibling.Start < nextDayStart
                    && sibling.Start < row.Start,
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
            row.Start,
            row.End,
            themes,
            speakers,
            seats,
            row.CategoryId,
            row.CategoryName,
            row.CategoryNameArabic,
            row.Status,
            row.PublishedAt,
            // The app shows a player only for a published
            // session that actually has a recording.
            row.Status == SessionStatus.Published && row.HasRecordingFile,
            // §8: the live broadcast feed(s) — null when the session is not live.
            row.LiveStreamUrl,
            row.LiveSignLanguageUrl,
            // The AI live-caption text (null when none set).
            row.LiveCaptions,
            row.LiveCaptionsArabic,
            // The gold-badge ordinal (1-based position within the day).
            displayOrder,
            // Website Session-detail: outcomes + language label.
            outcomes,
            row.Language,
            row.LanguageArabic,
            // "روابط التحميل" — the session's public downloadable files.
            downloads,
            // The session kind, so the app can reduce a WORKSHOP's detail to
            // title + time. Without it the client read json['type'] as null on
            // every session and the branch could never fire.
            row.Type,
            // The grace the door will actually apply, resolved by the SAME
            // rule the door uses so the app's check-in hint and the server's answer
            // cannot disagree.
            WalkInModeOptions.ResolveArrivalGraceMinutes(
                row.ArrivalGraceMinutesOverride,
                row.HallArrivalGraceMinutes,
                walkInMode.CurrentValue.ResolveArrivalGraceMinutes(timeProvider.SimfNow())),
            // The informational live notice (null when the admin set none).
            // Served alongside the feed above, which stays available to everyone.
            row.LiveNotice,
            row.LiveNoticeArabic);
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
                && session.RecordingFileId != null)
            .Select(session => new
            {
                session.RecordingFileId,
                session.RecordingContentType,
                session.RecordingFileName,
            })
            .SingleOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new SessionRecordingRef(
                row.RecordingFileId!.Value,
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

        // Owner 2026-07-19 (two-path Q&A): the recorded archive is the questions
        // that were actually ASKED on stage — i.e. pushed to the speaker by the
        // moderator (IsPushed) — not every Approved row. Since a live question now
        // lands Approved directly (skipping the committee), an Approved filter here
        // would leak live questions the moderator never surfaced; a hide clears the
        // push flag, and a push requires Approved, so IsPushed is exactly the set of
        // moderator-surfaced, not-since-hidden questions for both paths.
        var rows = await dbContext.SessionQuestions
            .AsNoTracking()
            .Where(q => q.SessionId == id && q.IsPushed)
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

        // Attribute to the asker via the Identity DB — a second query, never a
        // cross-database JOIN.
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
        // A محضر is viewable only once the session has actually
        // STARTED (in-progress or finished); it stays hidden before the session
        // begins. Keyed on the CLOCK (now >= Start), never the manual Held flag,
        // because "logically you can't view a summary before the session starts".
        var now = timeProvider.SimfNow();
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
                && summary.Session!.Start <= now)
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
                summary.PublishedAt!.Value,
                // The two videos on the summary surface:
                // the session's FULL live recording (Session.LiveStreamUrl — the
                // YouTube/HLS feed that doubles as the recording; no schema change)
                // and the team's OPTIONAL short summary cut. Each is null when
                // unset, and the app hides that player.
                summary.Session!.LiveStreamUrl,
                summary.SummaryVideoUrl))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<HostSessionSummary?> GetApprovedSummaryForHostAsync(
        Guid callerUserId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        // Authz: only the session's moderator (the SessionModerator grant)
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
