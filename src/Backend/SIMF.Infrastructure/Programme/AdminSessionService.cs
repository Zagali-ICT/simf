// Tests: SIMF.Api.Tests/AdminSessionsTests.cs,
//        SIMF.Api.Tests/GridDateSortKeyTests.cs,
//        SIMF.Api.Tests/SessionLifecycleTests.cs,
//        SIMF.Api.Tests/SessionRecordingTests.cs,
//        SIMF.Api.Tests/SessionLiveNoticeTests.cs
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIMF.Application.Auditing;
using SIMF.Application.Files.Abstractions;
using SIMF.Application.Notifications;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Grids;
using SIMF.Common.Options;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Sessions;
using SIMF.Domain.Programme;
using SIMF.Domain.SeatReservations;
using SIMF.Infrastructure.Common.Grids;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Programme;

/// <summary>
/// Admin CRUD over <see cref="Session"/>.
/// Real DB FK to <see cref="Hall"/>, M-to-M joins via
/// <see cref="SessionSpeaker"/> + <see cref="SessionTheme"/>. Effective
/// capacity = <c>CapacityOverride ?? Hall.SeatCount</c> — a reconfigured
/// room overrides it; most sessions inherit.
/// </summary>
internal sealed class AdminSessionService(
    SimfAppDbContext dbContext,
    IFeedLinkService feedLinks,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    IFileService fileService,
    INotificationDispatcher notifications,
    IOptionsMonitor<WalkInModeOptions> walkInMode,   // the grace fallback
    ILogger<AdminSessionService> logger) : IAdminSessionService
{
    /// <summary>The grace a session inherits when neither it nor its hall
    /// sets one. Read per call so arming the walk-in mode needs no restart.</summary>
    private int GlobalArrivalGraceMinutes =>
        walkInMode.CurrentValue.ResolveArrivalGraceMinutes(timeProvider.SimfNow());

    // The page that can actually release a held seat. The old copy sent the
    // admin to the Bookings desk, which is an explicitly read-only monitor with no
    // row actions, so the instruction could not be carried out. Named once here so
    // the deactivate-via-update and the delete path stay in step.
    private const string SeatPlanPageEnglish =
        "the Session seat plans page (/admin/sessions/seat-plans)";
    private const string SeatPlanPageArabic =
        "صفحة مخططات مقاعد الجلسات (/admin/sessions/seat-plans)";

    /// <summary>
    /// The grid contract for /admin/sessions: one entry per key SessionsList.razor
    /// can send, plus the two the hall-schedule panel sends
    /// (<c>hallId</c> + <c>isActive</c>). A key not declared here is a 400, not a
    /// silently ignored request.
    /// </summary>
    private static readonly GridColumns<Session> Columns = new GridColumns<Session>()
        .Add("code", session => session.Code, searchable: true)
        .Add("title", session => session.Title, searchable: true)
        .Add("titleArabic", session => session.TitleArabic, searchable: true)
        .Add("start", session => session.Start)
        .Add("end", session => session.End)
        .Add("isActive", session => session.IsActive)
        .Add("hallId", session => session.HallId)
        // "Sessions for this speaker" — the Speakers grid deep-links here with
        // ?speakerId={id}. The session↔speaker link is the M-to-M SessionSpeaker
        // set, so this is a navigation subquery and not a column comparison.
        .AddFilter("speakerId", raw =>
        {
            if (!Guid.TryParse(raw, out var speakerId))
            {
                throw GridFilters.ValueInvalid("speakerId", raw, "a speaker id (GUID)");
            }
            return session => session.Speakers.Any(link => link.SpeakerId == speakerId);
        })
        .DefaultOrder("start")
        .PageSize(fallback: 25, max: 200);

    public Task<GridPage<AdminSessionSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        // Hoisted to a local so it rides the query as a parameter (the option
        // monitor cannot be translated to SQL).
        var globalGraceMinutes = GlobalArrivalGraceMinutes;

        // Built per call rather than held in a static field: it closes over the
        // grace value above and reads StoredFiles off this instance's context.
        Expression<Func<Session, AdminSessionSummary>> toSummary =
            session => new AdminSessionSummary(
                session.Id,
                session.Code,
                session.Title,
                session.TitleArabic,
                session.HallId,
                session.Hall!.Name,
                session.Hall!.NameArabic,
                session.Start,
                session.End,
                session.CapacityOverride ?? session.Hall!.Capacity,
                session.IsActive,
                session.CreatedAt,
                session.CategoryId,
                session.Status,
                session.Type,
                // Carried so the grid Excel export round-trips them.
                session.Description,
                session.DescriptionArabic,
                // The feeds are file-store rows; the wire keeps carrying the URL
                // itself because both clients classify a feed by reading it.
                dbContext.StoredFiles
                    .Where(file => file.Id == session.LiveStreamFileId && file.IsActive)
                    .Select(file => file.ExternalUrl)
                    .FirstOrDefault(),
                dbContext.StoredFiles
                    .Where(file =>
                        file.Id == session.LiveSignLanguageFileId && file.IsActive)
                    .Select(file => file.ExternalUrl)
                    .FirstOrDefault(),
                session.LiveCaptions,
                session.LiveCaptionsArabic,
                session.SeatSelectionModeOverride,
                // Same reason: the Excel lane is the only bulk authoring
                // path, so a notice absent here is a notice destroyed by a
                // round-trip through it.
                session.LiveNotice,
                session.LiveNoticeArabic,
                // The raw override for the Excel round-trip, then the
                // resolved value the Hall-Arrivals console filters its session
                // picker by. The SAME shared rule the hall door applies, so the
                // picker and the door cannot disagree about which sessions are
                // open; evaluated client-side in this top-level projection.
                session.ArrivalGraceMinutesOverride,
                WalkInModeOptions.ResolveArrivalGraceMinutes(
                    session.ArrivalGraceMinutesOverride,
                    session.Hall!.ArrivalGraceMinutes,
                    globalGraceMinutes));

        return dbContext.Sessions.ToGridPageAsync(
            query, Columns, session => session.Id, toSummary, cancellationToken);
    }

    public async Task<AdminSessionDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var session = await dbContext.Sessions
            .AsNoTracking()
            .Include(row => row.Hall)
            .Include(row => row.Speakers).ThenInclude(speakerLink => speakerLink.Speaker)
            .Include(row => row.Themes)
            .Include(row => row.Outcomes)
            .Include(row => row.RecordingFile)
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        if (session is null)
        {
            return null;
        }
        // Stamp the current seat holding so the CP edit form can warn,
        // with a real number, that changing the hall or the window will destroy
        // it. One grouped round-trip; no extra endpoint and no extra permission.
        var (reservations, adminBlocks) = await CountActiveHoldingAsync(id, cancellationToken);
        return (await ToDetailAsync(session, cancellationToken)) with
        {
            ActiveReservationCount = reservations,
            ActiveAdminBlockCount = adminBlocks,
        };
    }

    /// <summary>The session's live seat holding split into the two things a
    /// hall-or-time change destroys: visitor registrations (an attendee to notify)
    /// and admin row-blocks (no attendee — the admin must re-paint them). Released
    /// rows are excluded; a single grouped query serves both counts.</summary>
    private async Task<(int Reservations, int AdminBlocks)> CountActiveHoldingAsync(
        Guid sessionId, CancellationToken cancellationToken)
    {
        var held = await dbContext.SeatReservations
            .AsNoTracking()
            .Where(reservation =>
                reservation.SessionId == sessionId && reservation.ReleasedAt == null)
            .GroupBy(reservation => reservation.ReservedForProfileId == null)
            .Select(group => new { IsAdminBlock = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        return (
            held.SingleOrDefault(row => !row.IsAdminBlock)?.Count ?? 0,
            held.SingleOrDefault(row => row.IsAdminBlock)?.Count ?? 0);
    }

    public async Task<AdminSessionDetail> CreateAsync(
        Guid actorUserId,
        AdminCreateSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var (code, title, titleArabic) = ValidateAndNormalise(
            request.Code, request.Title, request.TitleArabic);
        ValidateTimeWindow(request.Start, request.End);
        ValidateCapacity(request.CapacityOverride);
        EnsureArrivalGraceInRange(request.ArrivalGraceMinutesOverride);
        ValidateLiveUrls(request.LiveStreamUrl, request.LiveSignLanguageUrl);
        ValidateTextLengths(
            request.Description, request.DescriptionArabic,
            request.LiveCaptions, request.LiveCaptionsArabic,
            request.LiveStreamUrl, request.LiveSignLanguageUrl,
            request.LiveNotice, request.LiveNoticeArabic);
        ValidateSessionDetailFields(
            request.Language, request.LanguageArabic, request.Outcomes);

        var hall = await ResolveHallAsync(request.HallId, cancellationToken);
        await EnsureSpeakersExistAsync(request.Speakers, cancellationToken);
        await EnsureThemesExistAsync(request.ThemeIds, cancellationToken);
        await EnsureCategoryIsValidAsync(request.CategoryId, cancellationToken);

        if (request.Type is null)
        {
            throw new ApiException(
                ErrorCodes.SessionTypeRequired, 400,
                "A session type is required (Workshop, Session or Event).",
                "نوع الجلسة مطلوب (ورشة عمل أو جلسة أو حدث).");
        }
        if (!SatisfiesSpeakerRule(request.Type, request.Speakers.Count))
        {
            throw new ApiException(
                ErrorCodes.SessionSpeakerRequired, 400,
                "A non-event session must have at least one speaker.",
                "يجب أن يكون للجلسة (غير الحدث) متحدّث واحد على الأقل.");
        }

        var now = timeProvider.SimfNow();
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = code,
            Title = title,
            TitleArabic = titleArabic,
            Description = NullIfBlank(request.Description),
            DescriptionArabic = NullIfBlank(request.DescriptionArabic),
            // Website Session-detail "at a glance" language label.
            Language = NullIfBlank(request.Language),
            LanguageArabic = NullIfBlank(request.LanguageArabic),
            HallId = hall.Id,
            CategoryId = request.CategoryId,
            // Session type for the app's type tabs.
            Type = request.Type,
            // Optional per-session seat-selection-mode override.
            SeatSelectionModeOverride = request.SeatSelectionModeOverride,
            ArrivalGraceMinutesOverride = request.ArrivalGraceMinutesOverride,
            Start = request.Start,
            End = request.End,
            CapacityOverride = request.CapacityOverride,
            // The live feeds are set after the row exists, below: a file needs an
            // owner id, and the session has none until it is saved.
            // AI live-caption text (manual stub provider, bilingual).
            LiveCaptions = NullIfBlank(request.LiveCaptions),
            LiveCaptionsArabic = NullIfBlank(request.LiveCaptionsArabic),
            // The informational live notice (bilingual). Blank = no notice;
            // it never gates the feed.
            LiveNotice = NullIfBlank(request.LiveNotice),
            LiveNoticeArabic = NullIfBlank(request.LiveNoticeArabic),
            IsActive = true,
            CreatedAt = now,
        };
        foreach (var entry in request.Speakers)
        {
            session.Speakers.Add(new SessionSpeaker
            {
                SessionId = session.Id,
                SpeakerId = entry.SpeakerId,
                DisplayOrder = entry.DisplayOrder,
                Role = entry.Role,
            });
        }
        foreach (var themeId in request.ThemeIds.Distinct())
        {
            session.Themes.Add(new SessionTheme
            {
                SessionId = session.Id,
                ThemeId = themeId,
            });
        }
        // Website Session-detail "أبرز المخرجات" — renumber to a clean 0..n-1
        // order so the public read renders deterministically.
        var outcomeOrder = 0;
        foreach (var outcome in request.Outcomes)
        {
            session.Outcomes.Add(new SessionOutcome
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                Text = outcome.Text.Trim(),
                TextArabic = outcome.TextArabic.Trim(),
                DisplayOrder = outcomeOrder++,
                IsActive = true,
                // CreatedAt/CreatedBy are stamped by the audit interceptor (as in
                // ReplaceOutcomes) — no explicit stamp needed here.
            });
        }
        // Close the read-then-insert race on the one-session-per-hall-slot rule. A
        // time RANGE cannot be a unique constraint, so the overlap scan and the
        // insert must run inside ONE Serializable transaction: the scan then holds
        // key-range locks and a rival insert into the same slot cannot slip in
        // between the check and the save. Two admins creating differently-coded
        // sessions for the same hall and hour used to both pass an unlocked
        // AnyAsync and both commit, which silently breaks everything downstream
        // that assumes a hall runs one session at a time — the door's admitting set
        // and the per-session capacity count. The code-uniqueness scan joins them so
        // its own read is range-locked too (the unique index still backstops it).
        // Run through the EF execution strategy so it composes with
        // EnableRetryOnFailure — a manual transaction throws under the retrying
        // strategy otherwise — and a serialization/deadlock victim re-runs the whole
        // unit against the committed rival and raises the clean 409. The session is
        // built once above with a fixed Id, so a retry re-inserts the same row
        // instead of duplicating it. Mirrors BusinessMeetingService.ScheduleAsync.
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellationToken);

            await EnsureNoHallTimeOverlapAsync(
                hall.Id, request.Start, request.End, session.Id, cancellationToken);
            await EnsureCodeIsFreeAsync(code, session.Id, cancellationToken);

            dbContext.Sessions.Add(session);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });

        // After the save, because a file row needs a real owner id and the session
        // has none until it exists. A blank URL leaves the pointer null.
        session.LiveStreamFileId = await feedLinks.SetAsync(
            FileService.SessionLiveStream, session.Id,
            request.LiveStreamUrl, actorUserId, cancellationToken);
        session.LiveSignLanguageFileId = await feedLinks.SetAsync(
            FileService.SessionSignLanguage, session.Id,
            request.LiveSignLanguageUrl, actorUserId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.SessionCreated,
            actorUserId,
            $"id={session.Id}; code={code}; hall={hall.Id}",
            cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} created Session {Code} ({Id})",
            actorUserId, code, session.Id);

        return (await GetAsync(session.Id, cancellationToken))!;
    }

    public async Task<AdminSessionDetail> UpdateAsync(
        Guid actorUserId,
        Guid id,
        AdminUpdateSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = await dbContext.Sessions
            .Include(row => row.Speakers)
            .Include(row => row.Themes)
            .Include(row => row.Outcomes)
            .Include(row => row.RecordingFile)
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.SessionNotFound, 404,
                "The session was not found.",
                "لم يتم العثور على الجلسة.");

        var (code, title, titleArabic) = ValidateAndNormalise(
            request.Code, request.Title, request.TitleArabic);
        ValidateTimeWindow(request.Start, request.End);
        ValidateCapacity(request.CapacityOverride);
        EnsureArrivalGraceInRange(request.ArrivalGraceMinutesOverride);
        ValidateLiveUrls(request.LiveStreamUrl, request.LiveSignLanguageUrl);
        ValidateTextLengths(
            request.Description, request.DescriptionArabic,
            request.LiveCaptions, request.LiveCaptionsArabic,
            request.LiveStreamUrl, request.LiveSignLanguageUrl,
            request.LiveNotice, request.LiveNoticeArabic);
        ValidateSessionDetailFields(
            request.Language, request.LanguageArabic, request.Outcomes);

        var hall = await ResolveHallAsync(request.HallId, cancellationToken);
        await EnsureSpeakersExistAsync(request.Speakers, cancellationToken);
        await EnsureThemesExistAsync(request.ThemeIds, cancellationToken);
        await EnsureCategoryIsValidAsync(request.CategoryId, cancellationToken);

        // #3 (grandfathered) — a set type cannot be cleared back to null; a legacy
        // untyped row (session.Type == null) may stay untyped so an unrelated edit
        // is not blocked. session.Type still holds the stored value here.
        if (request.Type is null && session.Type is not null)
        {
            throw new ApiException(
                ErrorCodes.SessionTypeRequired, 400,
                "A session type is required (Workshop, Session or Event).",
                "نوع الجلسة مطلوب (ورشة عمل أو جلسة أو حدث).");
        }
        // #4 (grandfathered, no-regression) — a session that already satisfied the
        // speaker rule must keep satisfying it; a legacy non-Event row with no
        // speakers may stay as-is. session.Speakers still holds the stored roster.
        if (SatisfiesSpeakerRule(session.Type, session.Speakers.Count)
            && !SatisfiesSpeakerRule(request.Type, request.Speakers.Count))
        {
            throw new ApiException(
                ErrorCodes.SessionSpeakerRequired, 400,
                "A non-event session must have at least one speaker.",
                "يجب أن يكون للجلسة (غير الحدث) متحدّث واحد على الأقل.");
        }

        // S-1 (owner default) — capture whether the hall or the time window is
        // changing BEFORE the fields are overwritten; either invalidates held seats.
        var hallChanged = session.HallId != hall.Id;
        var timeChanged = session.Start != request.Start
            || session.End != request.End;

        // Soft-deleting via update (IsActive true -> false) must not orphan
        // active visitor bookings (same rule as DeactivateAsync).
        var deactivating = session.IsActive && !request.IsActive;
        // The mirror case: a row coming BACK from inactive re-enters the hall
        // schedule, and the overlap check ignores inactive rows — so the slot it
        // used to hold may have been taken while it was away. Captured here, beside
        // the hall/time flags and before IsActive is overwritten, because after the
        // assignment below the stored value is gone.
        var reactivating = !session.IsActive && request.IsActive;
        if (deactivating)
        {
            var heldVisitorBookings = await dbContext.SeatReservations
                .CountAsync(reservation =>
                    reservation.SessionId == id
                    && reservation.ReleasedAt == null
                    && reservation.ReservedForProfileId != null,
                    cancellationToken);
            if (heldVisitorBookings > 0)
            {
                throw new ApiException(
                    ErrorCodes.SessionHasActiveBookings, 409,
                    // Name the page that can actually do it. The Bookings monitor
                    // (/admin/bookings) is read-only with no row actions; releasing a
                    // held seat is done on the Session seat plans page.
                    $"This session has {heldVisitorBookings} active booking(s) — release them on {SeatPlanPageEnglish} before deactivating it.",
                    $"لهذه الجلسة {heldVisitorBookings} حجز نشط — يجب تحريرها من {SeatPlanPageArabic} قبل إلغاء تفعيلها.");
            }
        }

        // Never shrink the effective capacity below the seats already held
        // (visitor bookings + admin row-blocks), which would silently oversell.
        // Effective capacity = CapacityOverride ?? Hall.Capacity, so CLEARING the
        // override to null lowers it exactly like reducing it — a null override
        // must not bypass this guard; resolve the effective value and check that,
        // never just the non-null override. Skipped when the hall or time is
        // changing, because that path cascade-releases every held seat anyway.
        if (!(hallChanged || timeChanged))
        {
            var effectiveCapacity = request.CapacityOverride ?? hall.Capacity;
            var heldSeatCount = await dbContext.SeatReservations
                .CountAsync(reservation =>
                    reservation.SessionId == id && reservation.ReleasedAt == null,
                    cancellationToken);
            if (effectiveCapacity < heldSeatCount)
            {
                throw new ApiException(
                    ErrorCodes.SessionCapacityBelowBookings, 409,
                    $"The effective capacity ({effectiveCapacity}) is below the {heldSeatCount} seat(s) already held.",
                    $"السعة الفعّالة ({effectiveCapacity}) أقل من {heldSeatCount} مقعد محجوز بالفعل.");
            }
        }

        if (!string.Equals(session.Code, code, StringComparison.OrdinalIgnoreCase))
        {
            await EnsureCodeIsFreeAsync(code, id, cancellationToken);
        }

        // Checked when the slot actually moves, and when the row RE-ENTERS the
        // schedule: re-ticking Active changes neither hall nor time, but the check
        // that let a sibling take this slot skipped this row precisely because it
        // was inactive, so saving it active again would put two active sessions in
        // one hall at one time through the ordinary edit form, with no race at all.
        // Still not run on a plain edit: a title-only save of a session with a
        // pre-existing overlapping sibling (legacy data) must stay saveable, and
        // deactivation must not be blocked.
        if (hallChanged || timeChanged || reactivating)
        {
            await EnsureNoHallTimeOverlapAsync(
                hall.Id, request.Start, request.End, id, cancellationToken);
        }

        // Unticking Active on the edit form cancels the session exactly as the
        // Deactivate action does, so it owes the same notice. Resolve the audience
        // BEFORE the row is hidden and before the cascade releases anything; the
        // dispatch itself happens after the commit, in the shared announce step.
        List<Guid> cancellationAudience = [];
        if (deactivating)
        {
            cancellationAudience = await ResolveCancellationAudienceAsync(id, cancellationToken);
        }

        // The live feeds become file-store rows, which validates each URL against
        // the same rule the players apply instead of storing whatever was typed.
        // Resolved BEFORE the first session field is assigned: the file store
        // commits through its OWN SaveChanges on this same context (and its owner
        // pointer sync saves again), so running it mid-mutation flushed half of this
        // update — the new code, hall, start and end — outside the transaction
        // below, which is both a half-applied write when the store then rejects a
        // URL and a slot move that no lock ever covered.
        var liveStreamFileId = await feedLinks.SetAsync(
            FileService.SessionLiveStream, session.Id,
            request.LiveStreamUrl, actorUserId, cancellationToken);
        var liveSignLanguageFileId = await feedLinks.SetAsync(
            FileService.SessionSignLanguage, session.Id,
            request.LiveSignLanguageUrl, actorUserId, cancellationToken);

        session.Code = code;
        session.Title = title;
        session.TitleArabic = titleArabic;
        session.Description = NullIfBlank(request.Description);
        session.DescriptionArabic = NullIfBlank(request.DescriptionArabic);
        // Website Session-detail "at a glance" language label.
        session.Language = NullIfBlank(request.Language);
        session.LanguageArabic = NullIfBlank(request.LanguageArabic);
        session.HallId = hall.Id;
        session.CategoryId = request.CategoryId;
        session.Type = request.Type;
        session.SeatSelectionModeOverride = request.SeatSelectionModeOverride;
        session.ArrivalGraceMinutesOverride = request.ArrivalGraceMinutesOverride;
        session.Start = request.Start;
        session.End = request.End;
        // A rescheduled session must stay remindable and rateable. Both workers
        // treat a non-null stamp as "already done" (SessionReminderWorker filters on
        // ReminderSentAt == null, SessionRatingPromptWorker on RatingPromptSentAt ==
        // null), so a session moved after its reminder fired would never fire again
        // for the new time. Cleared ONLY when the window actually moves — an unrelated
        // save (title, speakers, captions) must not re-arm an already-sent reminder.
        if (timeChanged)
        {
            session.ReminderSentAt = null;
            session.RatingPromptSentAt = null;
        }
        session.CapacityOverride = request.CapacityOverride;
        session.LiveStreamFileId = liveStreamFileId;
        session.LiveSignLanguageFileId = liveSignLanguageFileId;
        // AI live-caption text (manual stub provider, bilingual).
        session.LiveCaptions = NullIfBlank(request.LiveCaptions);
        session.LiveCaptionsArabic = NullIfBlank(request.LiveCaptionsArabic);
        // The informational live notice (bilingual). Clearing the CP input
        // stores null again, which simply removes the banner.
        session.LiveNotice = NullIfBlank(request.LiveNotice);
        session.LiveNoticeArabic = NullIfBlank(request.LiveNoticeArabic);
        session.IsActive = request.IsActive;
        session.UpdatedAt = timeProvider.SimfNow();

        ReplaceSpeakerLinks(session, request.Speakers);
        ReplaceThemeLinks(session, request.ThemeIds);
        ReplaceOutcomes(session, request.Outcomes);

        // S-1 (owner default) — a hall move or a start/end change invalidates every
        // held seat (a seat belongs to a specific hall + time slot), so cascade-release
        // the active reservations in the SAME unit of work and notify the affected
        // visitors after the commit.
        List<SeatReservation> releasedReservations = [];
        if (hallChanged || timeChanged)
        {
            releasedReservations = await dbContext.SeatReservations
                .Where(reservation =>
                    reservation.SessionId == id && reservation.ReleasedAt == null)
                .ToListAsync(cancellationToken);
            foreach (var reservation in releasedReservations)
            {
                reservation.ReleasedAt = session.UpdatedAt!.Value;
                reservation.Status = BookingStatus.Cancelled;
            }
        }

        // The slot guard above answers the admin early — before the file-store
        // writes — but it holds no lock, so two admins moving sessions into the same
        // free slot at the same moment could both pass it and both commit. The check
        // is therefore REPEATED here, inside one Serializable transaction with the
        // write: the range scan holds key-range locks, so the loser is rejected with
        // the same clean 409 instead of committing a second active session into the
        // hall. Run through the EF execution strategy so it composes with
        // EnableRetryOnFailure (a manual transaction throws under the retrying
        // strategy) and a serialization/deadlock victim re-runs the unit against the
        // committed rival. Same pattern as CreateAsync above.
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellationToken);

            if (hallChanged || timeChanged || reactivating)
            {
                await EnsureNoHallTimeOverlapAsync(
                    hall.Id, request.Start, request.End, id, cancellationToken);
            }
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });

        await auditLog.WriteSuccessAsync(
            AuditEvents.SessionUpdated,
            actorUserId,
            $"id={session.Id}; code={code}; active={session.IsActive}",
            cancellationToken);

        // The cascade release needs a trace of its own: the generic "was updated"
        // toast says nothing, and admin row-blocks have no attendee, so even the
        // notify path early-returns on them. Record WHAT was destroyed as its own
        // audit row, and hand the counts back on the response so the Control Panel
        // can say so.
        var releasedForVisitors = releasedReservations
            .Count(reservation => reservation.ReservedForProfileId is not null);
        var releasedAdminBlocks = releasedReservations.Count - releasedForVisitors;
        if (releasedReservations.Count > 0)
        {
            await auditLog.WriteSuccessAsync(
                AuditEvents.SeatReservationReleased,
                actorUserId,
                $"session={session.Id}; code={code}; reason="
                    + $"{(hallChanged ? "HallChanged" : "Rescheduled")}; "
                    + $"reservations={releasedForVisitors}; adminBlocks={releasedAdminBlocks}",
                cancellationToken);
        }

        // Notify each affected visitor AFTER the commit (best-effort; the
        // dispatcher writes to the Identity DB via its own context). Resolve every
        // holder's ACCOUNT in ONE query first: a keynote in an 800-seat hall
        // released 800 rows and this loop used to issue a profile lookup per row,
        // holding the request open for 800 sequential round-trips before the first
        // notice was even sent.
        var recipientsByProfileId =
            await ResolveReleasedSeatRecipientsAsync(releasedReservations, cancellationToken);
        foreach (var reservation in releasedReservations)
        {
            await TryNotifyBookingReleasedAsync(
                reservation, session, recipientsByProfileId, cancellationToken);
        }

        // The edit form's Active checkbox is a fully reachable cancellation path,
        // so it takes the SAME announce step as DeactivateAsync: the notified=N audit
        // row plus the bilingual in-app + email notice. Without this the admin who
        // unticks Active instead of pressing Deactivate reproduces the exact silence
        // this step was written to end.
        if (deactivating)
        {
            await AnnounceSessionCancelledAsync(
                actorUserId, session, cancellationAudience, cancellationToken);
        }

        return (await GetAsync(session.Id, cancellationToken))! with
        {
            ReleasedReservationCount = releasedForVisitors,
            ReleasedAdminBlockCount = releasedAdminBlocks,
        };
    }

    public async Task DeactivateAsync(
        Guid actorUserId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var session = await dbContext.Sessions
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.SessionNotFound, 404,
                "The session was not found.",
                "لم يتم العثور على الجلسة.");

        if (!session.IsActive)
        {
            return; // idempotent
        }

        // S-1 (owner default) — block deleting a session that still has active
        // visitor bookings; they must be cancelled/rejected first (no silent orphan).
        var activeBookings = await dbContext.SeatReservations
            .CountAsync(reservation =>
                reservation.SessionId == id
                && reservation.ReleasedAt == null
                && reservation.ReservedForProfileId != null,
                cancellationToken);
        if (activeBookings > 0)
        {
            throw new ApiException(
                ErrorCodes.SessionHasActiveBookings, 409,
                // Name the page that can actually do it (see UpdateAsync).
                $"This session has {activeBookings} active booking(s) — release them on {SeatPlanPageEnglish} before deleting it.",
                $"لهذه الجلسة {activeBookings} حجز نشط — يجب تحريرها من {SeatPlanPageArabic} قبل حذفها.");
        }

        // Resolve the audience BEFORE the row is hidden. Without the notice the card
        // simply disappears from the app's "my sessions" list and the public agenda
        // with no message at all.
        var audience = await ResolveCancellationAudienceAsync(id, cancellationToken);

        session.IsActive = false;
        session.UpdatedAt = timeProvider.SimfNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await AnnounceSessionCancelledAsync(actorUserId, session, audience, cancellationToken);
    }

    /// <summary>The single "this session is cancelled" step, shared by BOTH ways an
    /// admin can cancel one: the Deactivate action and unticking Active on the edit form
    /// (<see cref="UpdateAsync"/>). Call it AFTER the commit, with an audience resolved
    /// BEFORE the row was hidden. Writes the SessionDeactivated audit row carrying
    /// notified=N, then tells each recipient — best-effort, because the dispatcher
    /// writes to the Identity DB via its own context and cannot share this
    /// transaction.</summary>
    private async Task AnnounceSessionCancelledAsync(
        Guid actorUserId,
        Session session,
        IReadOnlyList<Guid> audience,
        CancellationToken cancellationToken)
    {
        await auditLog.WriteSuccessAsync(
            AuditEvents.SessionDeactivated,
            actorUserId,
            $"id={session.Id}; code={session.Code}; notified={audience.Count}",
            cancellationToken);

        foreach (var userId in audience)
        {
            await TryNotifySessionCancelledAsync(userId, session, cancellationToken);
        }
    }

    /// <summary>Everyone who must be told a session was cancelled: the holders of
    /// an active seat reservation, plus everyone who favourited it. Both audiences lose
    /// the session card from their app agenda the moment the row is hidden, and neither
    /// was told anything before. Distinct ACCOUNT ids, because that is what a notice is
    /// delivered to; both queries stay on the App DB.
    ///
    /// <para>A booking is held by an attendee PROFILE, so the booked half joins
    /// through UserProfile to reach an account. A holder with no account drops out:
    /// the notice would have nowhere to go.</para></summary>
    private async Task<List<Guid>> ResolveCancellationAudienceAsync(
        Guid sessionId, CancellationToken cancellationToken)
    {
        var booked = await dbContext.SeatReservations
            .AsNoTracking()
            .Where(reservation =>
                reservation.SessionId == sessionId
                && reservation.ReleasedAt == null
                && reservation.ReservedForProfileId != null)
            .Join(dbContext.UserProfiles.AsNoTracking(),
                reservation => reservation.ReservedForProfileId!.Value,
                profile => profile.Id,
                (reservation, profile) => profile.UserId)
            .Where(userId => userId != null)
            .Select(userId => userId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var favourited = await dbContext.SessionFavourites
            .AsNoTracking()
            .Where(favourite => favourite.SessionId == sessionId)
            .Select(favourite => favourite.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return booked.Union(favourited).ToList();
    }

    /// <summary>The "this session was cancelled" notice: an in-app row AND an
    /// email (a visitor who never opens the app would otherwise turn up to a cancelled
    /// session). Bilingual; the time is the Saudi wall clock, never a zoned stamp. Best-effort —
    /// one failed recipient never aborts the rest, and the session is already
    /// cancelled either way.</summary>
    private async Task TryNotifySessionCancelledAsync(
        Guid userId, Session session, CancellationToken cancellationToken)
    {
        try
        {
            await notifications.DispatchAsync(new NotificationRequest
            {
                UserId = userId,
                Kind = NotificationKind.SessionCancelled,
                Title = "Session cancelled",
                TitleArabic = "تم إلغاء الجلسة",
                Body = $"\"{session.Title}\", scheduled for {session.Start.FormatSaudi()}, has been cancelled. It no longer appears in the programme.",
                BodyArabic = $"تم إلغاء جلسة \"{session.TitleArabic}\" المقرّرة بتاريخ {session.Start.FormatSaudi()}. لم تعد تظهر في البرنامج.",
                Severity = NotificationSeverity.Warning,
                RelatedEntityType = "Session",
                RelatedEntityId = session.Id,
                // No DeduplicateByRelatedEntity: the audience is already a distinct
                // set, so a user who both booked AND favourited is notified once, and
                // a genuine second cancellation (re-activated, then cancelled again)
                // must not be silently swallowed — that is the very failure this fixes.
                SendEmail = true,
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Session-cancelled notification failed for user {UserId} on session {SessionId}",
                userId, session.Id);
        }
    }

    // The legal adjacent lifecycle moves. Any pair not listed
    // (and not a same-status no-op) is rejected — so the Committee cannot
    // skip a step (e.g. Scheduled → Published) by hand.
    private static readonly HashSet<(SessionStatus From, SessionStatus To)> AllowedTransitions =
    [
        (SessionStatus.Scheduled, SessionStatus.Held),
        (SessionStatus.Held, SessionStatus.Scheduled),
        (SessionStatus.Held, SessionStatus.Recorded),
        (SessionStatus.Recorded, SessionStatus.Held),
        (SessionStatus.Recorded, SessionStatus.Published),
        (SessionStatus.Published, SessionStatus.Recorded),
    ];

    public async Task<AdminSessionDetail> SetStatusAsync(
        Guid actorUserId,
        Guid id,
        SessionStatus status,
        CancellationToken cancellationToken = default)
    {
        // Load the full graph once, tracked (we mutate it), so ToDetail builds
        // the DTO in memory after the save with no second round-trip — unlike
        // Create/Update, which omit these navigations and must re-fetch.
        var session = await LoadFullAsync(id, cancellationToken);

        var from = session.Status;
        if (from == status)
        {
            return await ToDetailAsync(session, cancellationToken); // idempotent — nothing changed
        }

        if (!AllowedTransitions.Contains((from, status)))
        {
            throw new ApiException(
                ErrorCodes.SessionStatusTransitionInvalid, 400,
                $"A session cannot move from {from} to {status}.",
                $"لا يمكن نقل الجلسة من {from} إلى {status}.");
        }

        var now = timeProvider.SimfNow();
        // Adjacency alone is not enough: a session cannot be marked Held
        // before it has started, nor Recorded/Published without an attached
        // recording. Default guard (no admin override in this increment).
        ValidateStatusGuards(session, status, now);
        session.Status = status;
        session.PublishedAt = status == SessionStatus.Published ? now : null;
        session.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        var eventType = status switch
        {
            SessionStatus.Published => AuditEvents.SessionPublished,
            SessionStatus.Recorded when from == SessionStatus.Published
                => AuditEvents.SessionUnpublished,
            _ => AuditEvents.SessionStatusChanged,
        };
        await auditLog.WriteSuccessAsync(
            eventType,
            actorUserId,
            $"id={session.Id}; code={session.Code}; {from}->{status}",
            cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} moved Session {Code} ({Id}) {From} -> {To}",
            actorUserId, session.Code, session.Id, from, status);

        return await ToDetailAsync(session, cancellationToken);
    }

    public async Task<AdminSessionDetail> UploadRecordingAsync(
        Guid actorUserId,
        Guid id,
        Stream content,
        string fileName,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadFullAsync(id, cancellationToken);

        // Stream the bytes into the unified StoredFile store (Internal
        // tier, plaintext + seekable for Range streaming, owner = the session). The
        // store never buffers the whole video and computes the SHA-256 on the fly;
        // RecordingFileId is the key + the "has recording" sentinel. The malware
        // scan is skipped (size-capped policy).
        var priorFileId = session.RecordingFileId;
        var result = await fileService.CreateStreamedAsync(
            FileService.SessionRecording, session.Id, content, fileName, contentType,
            System.IO.Path.GetExtension(fileName), actorUserId, cancellationToken);

        var now = timeProvider.SimfNow();
        // The key is the whole write. The store already recorded this file's name,
        // media type, size and uploader as part of CreateStreamedAsync, so there
        // is nothing else about it to keep here.
        session.RecordingFileId = result.Id;
        session.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        // Retire the prior recording (best-effort — the new one is the committed
        // source of truth; SessionRecording is DeletableDefault:true).
        await RetireRecordingAsync(priorFileId, result.Id, actorUserId, cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.SessionRecordingUploaded,
            actorUserId,
            $"id={session.Id}; code={session.Code}; file={fileName}; bytes={sizeBytes}",
            cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} uploaded recording for Session {Code} ({Id}), {Bytes} bytes",
            actorUserId, session.Code, session.Id, sizeBytes);

        return await ToDetailAsync(session, cancellationToken);
    }

    public async Task<AdminSessionDetail> DeleteRecordingAsync(
        Guid actorUserId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadFullAsync(id, cancellationToken);

        if (session.RecordingFileId is null)
        {
            return await ToDetailAsync(session, cancellationToken); // idempotent — nothing to delete
        }

        // The same guard ValidateStatusGuards applies on the way in: a Recorded or
        // Published session is defined by having a recording, so removing one here
        // would leave the row in a state the status move itself refuses to create
        // (and the app would offer a published recording that streams nothing).
        // Move the session back to Held first, which is the undo step for it.
        if (session.Status is SessionStatus.Recorded or SessionStatus.Published)
        {
            throw new ApiException(
                ErrorCodes.SessionStatusGuardFailed, 400,
                "A Recorded or Published session must keep its recording. Move it back to Held before deleting.",
                "يجب أن تحتفظ الجلسة المُسجّلة أو المنشورة بتسجيلها. أعدها إلى منعقدة قبل حذفه.");
        }

        var priorFileId = session.RecordingFileId;
        session.RecordingFileId = null;
        session.RecordingFile = null;
        session.UpdatedAt = timeProvider.SimfNow();
        // Clear the key first, then drop the file: if the file delete fails the
        // app already sees "no recording" and only an orphan file is left behind
        // (harmless), never a row pointing at a missing file.
        await dbContext.SaveChangesAsync(cancellationToken);
        await RetireRecordingAsync(priorFileId, null, actorUserId, cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.SessionRecordingDeleted,
            actorUserId,
            $"id={session.Id}; code={session.Code}",
            cancellationToken);

        return await ToDetailAsync(session, cancellationToken);
    }

    /// <summary>Best-effort retirement of a recording's <c>StoredFile</c>
    /// (soft-delete + byte-unlink). The row is already updated (source of truth), so a
    /// delete failure must not fail the operation — worst case leaves one orphan blob.
    /// No-op when there was no prior file, or it is the just-uploaded one.</summary>
    private async Task RetireRecordingAsync(
        Guid? priorFileId, Guid? newFileId, Guid actorUserId, CancellationToken cancellationToken)
    {
        if (priorFileId is not { } old || old == newFileId) { return; }
        try
        {
            await fileService.DeleteAsync(old, actorUserId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex, "Recording StoredFile retirement failed for file {FileId}; the session row is updated.", old);
        }
    }

    // -- helpers --------------------------------------------------------------

    // Loads the session with the navigations ToDetail needs, tracked
    // (callers mutate it). Shared by SetStatus / Upload / Delete recording.
    private async Task<Session> LoadFullAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.Sessions
            .Include(row => row.Hall)
            .Include(row => row.Speakers).ThenInclude(link => link.Speaker)
            .Include(row => row.Themes)
            .Include(row => row.Outcomes)
            // The recording's name, size and upload instant are read off the store
            // row now, so the detail projection needs it loaded.
            .Include(row => row.RecordingFile)
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
        ?? throw new ApiException(
            ErrorCodes.SessionNotFound, 404,
            "The session was not found.",
            "لم يتم العثور على الجلسة.");

    private static (string code, string title, string titleArabic) ValidateAndNormalise(
        string codeRaw, string titleRaw, string titleArabicRaw)
    {
        var code = (codeRaw ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length is < SessionRules.MinCodeLength or > SessionRules.MaxCodeLength)
        {
            throw new ApiException(
                ErrorCodes.SessionInvalid, 400,
                "Session code must be between 2 and 16 characters.",
                "يجب أن يتراوح طول رمز الجلسة بين 2 و 16 حرفاً.");
        }
        var title = (titleRaw ?? string.Empty).Trim();
        if (title.Length is < SessionRules.MinTitleLength or > SessionRules.MaxTitleLength)
        {
            throw new ApiException(
                ErrorCodes.SessionInvalid, 400,
                "Session English title must be between 1 and 256 characters.",
                "يجب أن يتراوح طول العنوان الإنجليزي للجلسة بين 1 و 256 حرفاً.");
        }
        var titleArabic = (titleArabicRaw ?? string.Empty).Trim();
        if (titleArabic.Length is < SessionRules.MinTitleLength or > SessionRules.MaxTitleLength)
        {
            throw new ApiException(
                ErrorCodes.SessionInvalid, 400,
                "Session Arabic title must be between 1 and 256 characters.",
                "يجب أن يتراوح طول العنوان العربي للجلسة بين 1 و 256 حرفاً.");
        }
        return (code, title, titleArabic);
    }

    // The clock + recording guards for a FORWARD lifecycle move. Reverse
    // (undo) moves (Held->Scheduled, Recorded->Held, Published->Recorded) carry no
    // guard. No admin override flag in this increment — an admin who must publish
    // pre-recorded content adjusts the session's start time / uploads a recording.
    private static void ValidateStatusGuards(
        Session session, SessionStatus target, DateTime now)
    {
        if (target == SessionStatus.Held && now < session.Start)
        {
            throw new ApiException(
                ErrorCodes.SessionStatusGuardFailed, 400,
                "A session cannot be marked Held before it has started.",
                "لا يمكن تعيين الجلسة كمنعقدة قبل أن تبدأ.");
        }
        if (target is SessionStatus.Recorded or SessionStatus.Published
            && session.RecordingFileId is null)
        {
            throw new ApiException(
                ErrorCodes.SessionStatusGuardFailed, 400,
                "A session needs an attached recording before it can be marked Recorded or Published.",
                "تحتاج الجلسة إلى تسجيل مرفق قبل تعيينها كمُسجّلة أو منشورة.");
        }
    }

    private static void ValidateTimeWindow(DateTime start, DateTime end)
    {
        if (end <= start)
        {
            throw new ApiException(
                ErrorCodes.SessionInvalidTimeWindow, 400,
                "Session end must be after its start.",
                "يجب أن تكون نهاية الجلسة بعد بدايتها.");
        }
    }

    private static void ValidateCapacity(int? capacityOverride)
    {
        if (capacityOverride is < 0)
        {
            throw new ApiException(
                ErrorCodes.SessionInvalid, 400,
                "Capacity override must be zero or a positive integer.",
                "يجب أن تكون السعة المخصصة صفراً أو عدداً صحيحاً موجباً.");
        }
    }

    /// <summary>The per-session arrival-grace override is optional (null =
    /// inherit the hall) but, when given, must be inside the one shared bound.
    /// Rejected rather than clamped, for the same reason as the hall's: quietly
    /// storing 240 when an admin typed 2400 would misreport how long the doors are
    /// open.</summary>
    private static void EnsureArrivalGraceInRange(int? minutes)
    {
        if (!WalkInModeOptions.IsValidArrivalGrace(minutes))
        {
            throw new ApiException(
                ErrorCodes.SessionInvalid, 400,
                $"Arrival grace must be between 0 and {WalkInModeOptions.MaxArrivalGraceMinutes} minutes.",
                $"يجب أن تكون مهلة الوصول بين 0 و{WalkInModeOptions.MaxArrivalGraceMinutes} دقيقة.");
        }
    }

    // A non-blank live feed URL must be a YouTube link or a direct
    // HLS/MP4 stream (the same rule the CP form enforces, LiveStreamUrlPolicy).
    // Blank stays "no feed" and is persisted as null (NullIfBlank below).
    private static void ValidateLiveUrls(string? liveStreamUrl, string? signLanguageUrl)
    {
        ValidateLiveUrl(liveStreamUrl,
            "The live stream URL must be a YouTube link or an HLS/MP4 stream.",
            "يجب أن يكون رابط البث المباشر رابط يوتيوب أو بثاً بصيغة HLS/MP4.");
        ValidateLiveUrl(signLanguageUrl,
            "The sign-language stream URL must be a YouTube link or an HLS/MP4 stream.",
            "يجب أن يكون رابط بث لغة الإشارة رابط يوتيوب أو بثاً بصيغة HLS/MP4.");
    }

    private static void ValidateLiveUrl(string? url, string englishMessage, string arabicMessage)
    {
        if (!string.IsNullOrWhiteSpace(url) && !LiveStreamUrlPolicy.IsAllowed(url))
        {
            throw new ApiException(
                ErrorCodes.SessionInvalid, 400, englishMessage, arabicMessage);
        }
    }

    // Validation triple-lock — the free-text + live-feed fields carry EF
    // column caps (SessionConfiguration: Description / DescriptionArabic /
    // LiveCaptions / LiveCaptionsArabic = nvarchar(2048); LiveStreamUrl /
    // LiveSignLanguageUrl = nvarchar(1024); LiveNotice / LiveNoticeArabic =
    // nvarchar(512)). Enforce those caps here so
    // over-length input returns a clean 400 instead of a SaveChanges
    // "string or binary data would be truncated" 500. Measured on the
    // trimmed value actually persisted by NullIfBlank, mirroring AdminHallService.
    private static void ValidateTextLengths(
        string? description, string? descriptionArabic,
        string? liveCaptions, string? liveCaptionsArabic,
        string? liveStreamUrl, string? liveSignLanguageUrl,
        string? liveNotice, string? liveNoticeArabic)
    {
        EnsureMaxLength(description, 2048,
            "The session description must be 2048 characters or fewer.",
            "يجب أن يكون وصف الجلسة 2048 حرفاً أو أقل.");
        EnsureMaxLength(descriptionArabic, 2048,
            "The Arabic session description must be 2048 characters or fewer.",
            "يجب أن يكون الوصف العربي للجلسة 2048 حرفاً أو أقل.");
        EnsureMaxLength(liveCaptions, 2048,
            "The live captions must be 2048 characters or fewer.",
            "يجب أن تكون التسميات التوضيحية للبث المباشر 2048 حرفاً أو أقل.");
        EnsureMaxLength(liveCaptionsArabic, 2048,
            "The Arabic live captions must be 2048 characters or fewer.",
            "يجب أن تكون التسميات التوضيحية العربية للبث المباشر 2048 حرفاً أو أقل.");
        EnsureMaxLength(liveStreamUrl, 1024,
            "The live stream URL must be 1024 characters or fewer.",
            "يجب أن يكون رابط البث المباشر 1024 حرفاً أو أقل.");
        EnsureMaxLength(liveSignLanguageUrl, 1024,
            "The sign-language stream URL must be 1024 characters or fewer.",
            "يجب أن يكون رابط بث لغة الإشارة 1024 حرفاً أو أقل.");
        // The informational live notice is a one-line banner (512).
        EnsureMaxLength(liveNotice, 512,
            "The live notice must be 512 characters or fewer.",
            "يجب أن يكون إشعار البث المباشر 512 حرفاً أو أقل.");
        EnsureMaxLength(liveNoticeArabic, 512,
            "The Arabic live notice must be 512 characters or fewer.",
            "يجب أن يكون الإشعار العربي للبث المباشر 512 حرفاً أو أقل.");
    }

    private static void EnsureMaxLength(
        string? value, int maxLength, string englishMessage, string arabicMessage)
    {
        if (value is not null && value.Trim().Length > maxLength)
        {
            throw new ApiException(
                ErrorCodes.SessionInvalid, 400, englishMessage, arabicMessage);
        }
    }

    // Two active sessions must not occupy the same hall at overlapping
    // times. Half-open overlap: existing.Start < newEnd AND newStart < existing.End.
    // Excludes the session being updated (excludeSessionId) and soft-deleted rows.
    private async Task EnsureNoHallTimeOverlapAsync(
        Guid hallId, DateTime start, DateTime end,
        Guid excludeSessionId, CancellationToken cancellationToken)
    {
        var overlaps = await dbContext.Sessions
            .AsNoTracking()
            .AnyAsync(other =>
                other.IsActive
                && other.HallId == hallId
                && other.Id != excludeSessionId
                && other.Start < end
                && start < other.End,
                cancellationToken);
        if (overlaps)
        {
            throw new ApiException(
                ErrorCodes.SessionHallTimeOverlap, 409,
                "Another active session already uses this hall at an overlapping time.",
                "توجد جلسة نشطة أخرى تستخدم هذه القاعة في وقت متداخل.");
        }
    }

    // A session code is unique across the table, INACTIVE rows included — the
    // unique index does not filter on IsActive, so a soft-deleted row still owns
    // its code and a clean 409 here is what stops the insert failing as a 500 at
    // the store. Create passes its own (not yet persisted) id, which excludes
    // nothing; update passes the row being edited so re-saving its own code is not
    // a clash.
    private async Task EnsureCodeIsFreeAsync(
        string code, Guid excludeSessionId, CancellationToken cancellationToken)
    {
        var clash = await dbContext.Sessions
            .AsNoTracking()
            .AnyAsync(row => row.Id != excludeSessionId && row.Code == code, cancellationToken);
        if (clash)
        {
            throw new ApiException(
                ErrorCodes.SessionCodeDuplicate, 409,
                $"A session with code '{code}' already exists.",
                $"توجد جلسة بالرمز '{code}' بالفعل.");
        }
    }

    private async Task<Hall> ResolveHallAsync(
        Guid hallId, CancellationToken cancellationToken)
    {
        var hall = await dbContext.Halls
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == hallId, cancellationToken);
        if (hall is null || !hall.IsActive)
        {
            throw new ApiException(
                ErrorCodes.SessionHallNotFound, 400,
                $"Hall '{hallId}' does not exist or is inactive.",
                $"القاعة '{hallId}' غير موجودة أو غير مفعّلة.");
        }
        return hall;
    }

    // A non-Event session must carry at least one speaker; an Event may have
    // none (an opening ceremony etc.). Kept as a pure predicate so create (strict)
    // and update (no-regression grandfather) both read from the one rule.
    private static bool SatisfiesSpeakerRule(SessionType? type, int speakerCount) =>
        type == SessionType.Event || speakerCount >= 1;

    private async Task EnsureSpeakersExistAsync(
        IList<AdminSessionSpeakerEntry> entries,
        CancellationToken cancellationToken)
    {
        if (entries.Count == 0) { return; }
        var ids = entries.Select(entry => entry.SpeakerId).Distinct().ToList();
        if (ids.Count != entries.Count)
        {
            throw new ApiException(
                ErrorCodes.SessionInvalid, 400,
                "A speaker can only be linked once per session.",
                "لا يمكن ربط المتحدّث بأكثر من مرّة في الجلسة الواحدة.");
        }
        var existing = await dbContext.Speakers
            .AsNoTracking()
            .Where(speaker => ids.Contains(speaker.Id) && speaker.IsActive)
            .Select(speaker => speaker.Id)
            .ToListAsync(cancellationToken);
        var missing = ids.Except(existing).ToList();
        if (missing.Count > 0)
        {
            throw new ApiException(
                ErrorCodes.SessionSpeakerNotFound, 400,
                $"Speakers not found or inactive: {string.Join(", ", missing)}.",
                $"المتحدّثون غير موجودين أو غير مفعّلين: {string.Join(", ", missing)}.");
        }
    }

    private async Task EnsureThemesExistAsync(
        IList<Guid> themeIds, CancellationToken cancellationToken)
    {
        if (themeIds.Count == 0) { return; }
        var ids = themeIds.Distinct().ToList();
        var existing = await dbContext.Themes
            .AsNoTracking()
            .Where(theme => ids.Contains(theme.Id) && theme.IsActive)
            .Select(theme => theme.Id)
            .ToListAsync(cancellationToken);
        var missing = ids.Except(existing).ToList();
        if (missing.Count > 0)
        {
            throw new ApiException(
                ErrorCodes.SessionThemeNotFound, 400,
                $"Themes not found or inactive: {string.Join(", ", missing)}.",
                $"المحاور غير موجودة أو غير مفعّلة: {string.Join(", ", missing)}.");
        }
    }

    // The session category, when set, must be an active row in the
    // dynamic SessionCategory lookup. Mirrors ResolveHallAsync's active check.
    private async Task EnsureCategoryIsValidAsync(
        Guid? categoryId, CancellationToken cancellationToken)
    {
        if (categoryId is null) { return; }
        var exists = await dbContext.SessionCategories
            .AsNoTracking()
            .AnyAsync(category => category.Id == categoryId.Value && category.IsActive, cancellationToken);
        if (!exists)
        {
            throw new ApiException(
                ErrorCodes.SessionCategoryInvalid, 400,
                $"Session category '{categoryId}' does not exist or is inactive.",
                $"تصنيف الجلسة '{categoryId}' غير موجود أو غير مفعّل.");
        }
    }

    private void ReplaceSpeakerLinks(
        Session session, IList<AdminSessionSpeakerEntry> entries)
    {
        dbContext.SessionSpeakers.RemoveRange(session.Speakers);
        session.Speakers.Clear();
        foreach (var entry in entries)
        {
            session.Speakers.Add(new SessionSpeaker
            {
                SessionId = session.Id,
                SpeakerId = entry.SpeakerId,
                DisplayOrder = entry.DisplayOrder,
                Role = entry.Role,
            });
        }
    }

    private void ReplaceThemeLinks(Session session, IList<Guid> themeIds)
    {
        dbContext.SessionThemes.RemoveRange(session.Themes);
        session.Themes.Clear();
        foreach (var themeId in themeIds.Distinct())
        {
            session.Themes.Add(new SessionTheme
            {
                SessionId = session.Id,
                ThemeId = themeId,
            });
        }
    }

    // Website Session-detail "أبرز المخرجات" — re-sync the session's outcome
    // bullets. Mirrors ReplaceSpeakerLinks: the explicit RemoveRange hard-deletes
    // the old rows in the same unit of work (clearing the nav alone would orphan
    // them), then the new set is re-added renumbered 0..n-1 for a stable order.
    private void ReplaceOutcomes(Session session, IList<AdminSessionOutcomeEntry> entries)
    {
        dbContext.SessionOutcomes.RemoveRange(session.Outcomes);
        session.Outcomes.Clear();
        var order = 0;
        foreach (var entry in entries)
        {
            session.Outcomes.Add(new SessionOutcome
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                Text = entry.Text.Trim(),
                TextArabic = entry.TextArabic.Trim(),
                DisplayOrder = order++,
                IsActive = true,
            });
        }
    }

    // Validation — the Website Session-detail fields: the bilingual language
    // label (EF nvarchar(64)) and each outcome bullet (EF nvarchar(512), both
    // languages required). Over-length / blank input → a clean 400 instead of a
    // SaveChanges truncation 500, mirroring ValidateTextLengths.
    private static void ValidateSessionDetailFields(
        string? language, string? languageArabic,
        IEnumerable<AdminSessionOutcomeEntry> outcomes)
    {
        EnsureMaxLength(language, 64,
            "The session language label must be 64 characters or fewer.",
            "يجب أن يكون وصف لغة الجلسة 64 حرفاً أو أقل.");
        EnsureMaxLength(languageArabic, 64,
            "The Arabic session language label must be 64 characters or fewer.",
            "يجب أن يكون وصف لغة الجلسة بالعربية 64 حرفاً أو أقل.");
        foreach (var outcome in outcomes)
        {
            if (string.IsNullOrWhiteSpace(outcome.Text)
                || string.IsNullOrWhiteSpace(outcome.TextArabic))
            {
                throw new ApiException(
                    ErrorCodes.SessionInvalid, 400,
                    "Each session outcome must have both English and Arabic text.",
                    "يجب أن يحتوي كل مخرج للجلسة على نص بالإنجليزية والعربية.");
            }
            EnsureMaxLength(outcome.Text, 512,
                "Each session outcome must be 512 characters or fewer.",
                "يجب أن يكون كل مخرج للجلسة 512 حرفاً أو أقل.");
            EnsureMaxLength(outcome.TextArabic, 512,
                "Each Arabic session outcome must be 512 characters or fewer.",
                "يجب أن يكون كل مخرج عربي للجلسة 512 حرفاً أو أقل.");
        }
    }

    /// <summary>The ACCOUNT behind each released seat, keyed by the holder's PROFILE
    /// id, in one query for the whole cascade. Admin row-blocks (no holder) and
    /// walk-ins (a profile with no account) are simply absent from the map, which is
    /// exactly the "nobody to tell" case the notify step already skips. Same shape as
    /// <see cref="ResolveCancellationAudienceAsync"/>: the join runs server-side and
    /// the null accounts are filtered there, never row by row in the loop.</summary>
    private async Task<IReadOnlyDictionary<Guid, Guid>> ResolveReleasedSeatRecipientsAsync(
        IReadOnlyList<SeatReservation> releasedReservations,
        CancellationToken cancellationToken)
    {
        var holderProfileIds = releasedReservations
            .Where(reservation => reservation.ReservedForProfileId is not null)
            .Select(reservation => reservation.ReservedForProfileId!.Value)
            .Distinct()
            .ToList();
        if (holderProfileIds.Count == 0)
        {
            return new Dictionary<Guid, Guid>();
        }

        var rows = await dbContext.UserProfiles
            .AsNoTracking()
            .Where(profile => holderProfileIds.Contains(profile.Id) && profile.UserId != null)
            .Select(profile => new { profile.Id, UserId = profile.UserId!.Value })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(row => row.Id, row => row.UserId);
    }

    // S-1 (owner default) — tell each affected visitor their held seat was released
    // because the session was moved or rescheduled. Reuses NotificationKind.BookingRejected
    // (in-app warning, session deep-link); the authoritative bilingual text is set below.
    private async Task TryNotifyBookingReleasedAsync(
        SeatReservation reservation,
        Session session,
        IReadOnlyDictionary<Guid, Guid> recipientsByProfileId,
        CancellationToken cancellationToken)
    {
        if (reservation.ReservedForProfileId is not { } holderProfileId)
        {
            return; // an admin row-block has no attendee to notify
        }

        // The notice goes to an ACCOUNT. A walk-in holds a seat and no account, so
        // there is nobody to tell (they are simply absent from the resolved map);
        // the release itself already stands.
        if (!recipientsByProfileId.TryGetValue(holderProfileId, out var recipientId))
        {
            return;
        }
        try
        {
            await notifications.DispatchAsync(new NotificationRequest
            {
                UserId = recipientId,
                Kind = NotificationKind.BookingRejected,
                Title = "Seat reservation released",
                TitleArabic = "تم إلغاء حجز المقعد",
                Body = $"\"{session.Title}\" was rescheduled or moved to another hall (it now starts {session.Start.FormatSaudi()}), so your seat was released. Please book again.",
                BodyArabic = $"تم تغيير موعد أو قاعة جلسة \"{session.TitleArabic}\" (تبدأ الآن {session.Start.FormatSaudi()})، لذا تم إلغاء حجز مقعدك. يرجى الحجز من جديد.",
                Severity = NotificationSeverity.Warning,
                RelatedEntityType = "Session",
                RelatedEntityId = session.Id,
                // An in-app row alone reached nobody who does not open the app,
                // and losing a booked seat is exactly the news that must not be
                // missed. Email it too (same dispatch convention as every other
                // must-not-miss kind: the dispatcher renders the body, no template).
                SendEmail = true,
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Booking-released notification failed for reservation {ReservationId}",
                reservation.Id);
        }
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<AdminSessionDetail> ToDetailAsync(
        Session session, CancellationToken cancellationToken)
    {
        var hallSeats = session.Hall?.Capacity ?? 0;
        var effective = session.CapacityOverride ?? hallSeats;
        // What this session would INHERIT if its override were cleared:
        // the hall's grace, else the global value. Deliberately EXCLUDES the
        // override itself, because the edit form offers this as "leave blank to
        // get this" — the resolved-including-override value would tell an admin
        // who has an override that clearing it keeps the same number.
        var inheritedGrace = WalkInModeOptions.ResolveArrivalGraceMinutes(
            null, session.Hall?.ArrivalGraceMinutes, GlobalArrivalGraceMinutes);
        var speakers = session.Speakers
            .OrderBy(link => link.DisplayOrder)
            .Select(link => new AdminSessionSpeakerEntry(
                link.SpeakerId,
                link.Speaker?.Name ?? string.Empty,
                link.Speaker?.NameArabic ?? string.Empty,
                link.DisplayOrder,
                link.Role))
            .ToList();
        var themeIds = session.Themes.Select(link => link.ThemeId).ToList();
        var outcomes = session.Outcomes
            .Where(outcome => outcome.IsActive)
            .OrderBy(outcome => outcome.DisplayOrder)
            .Select(outcome => new AdminSessionOutcomeEntry(
                outcome.Text, outcome.TextArabic, outcome.DisplayOrder))
            .ToList();
        return new AdminSessionDetail(
            session.Id,
            session.Code,
            session.Title,
            session.TitleArabic,
            session.Description,
            session.DescriptionArabic,
            session.HallId,
            session.Hall?.Name ?? string.Empty,
            session.Hall?.NameArabic ?? string.Empty,
            hallSeats,
            session.Start,
            session.End,
            session.CapacityOverride,
            effective,
            session.IsActive,
            speakers,
            themeIds,
            session.CreatedAt,
            session.UpdatedAt,
            session.CategoryId,
            session.Status,
            session.PublishedAt,
            session.RecordingFileId is not null,
            // Read off the store row rather than off a copy kept here. The
            // contract's field names are unchanged, so the Control Panel sees
            // exactly what it saw before - it is the source that moved.
            session.RecordingFile?.OriginalFileName,
            session.RecordingFile?.SizeBytes,
            session.RecordingFile?.CreatedAt,
            await feedLinks.ResolveAsync(session.LiveStreamFileId, cancellationToken),
            await feedLinks.ResolveAsync(session.LiveSignLanguageFileId, cancellationToken),
            // AI live-caption text.
            session.LiveCaptions,
            session.LiveCaptionsArabic,
            // Session type for the app's type tabs.
            session.Type,
            // Per-session seat-selection-mode override (null = inherit).
            session.SeatSelectionModeOverride,
            // Website Session-detail — language label + outcome bullets.
            session.Language,
            session.LanguageArabic,
            outcomes,
            // The informational live notice. Named so the four seat-holding
            // counts keep their defaults (GetAsync/UpdateAsync stamp those with a
            // `with` once the counts are known).
            LiveNotice: session.LiveNotice,
            LiveNoticeArabic: session.LiveNoticeArabic,
            // The stored override (null = inherit the hall) and the number
            // clearing it would fall back to, which is what the edit form names.
            ArrivalGraceMinutesOverride: session.ArrivalGraceMinutesOverride,
            InheritedArrivalGraceMinutes: inheritedGrace);
    }
}
