// Tests: SIMF.Api.Tests/AdminSessionsTests.cs (+ D-349 live-URL validation)
// Tests: SIMF.Api.Tests/SessionLifecycleTests.cs (P3.2a — D-231 lifecycle)
// Tests: SIMF.Api.Tests/SessionRecordingTests.cs (P3.2b — D-232 recording)
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Files.Abstractions;
using SIMF.Application.Notifications;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Domain.Programme;
using SIMF.Domain.SeatReservations;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Programme;

/// <summary>
/// D-165 (gap doc G3, PDF §2.9) — admin CRUD over <see cref="Session"/>.
/// Real DB FK to <see cref="Hall"/>, M-to-M joins via
/// <see cref="SessionSpeaker"/> + <see cref="SessionTheme"/>. Effective
/// capacity = <c>CapacityOverride ?? Hall.SeatCount</c> (PDF §2.9 —
/// reconfigured rooms override; most sessions inherit).
/// </summary>
internal sealed class AdminSessionService(
    SimfAppDbContext dbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    IFileService fileService,
    INotificationDispatcher notifications,
    ILogger<AdminSessionService> logger) : IAdminSessionService
{
    public async Task<GridPage<AdminSessionSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var (skip, top) = query.ClampPage(25, 200);

        var rows = dbContext.Sessions
            .AsNoTracking()
            .Include(session => session.Hall)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(session =>
                EF.Functions.Like(session.Code, $"%{term}%")
                || EF.Functions.Like(session.Title, $"%{term}%")
                || EF.Functions.Like(session.TitleArabic, $"%{term}%"));
        }
        if (query.Filters.TryGetValue("isActive", out var activeFilter)
            && bool.TryParse(activeFilter, out var isActive))
        {
            rows = rows.Where(session => session.IsActive == isActive);
        }
        if (query.Filters.TryGetValue("hallId", out var hallIdRaw)
            && Guid.TryParse(hallIdRaw, out var hallId))
        {
            rows = rows.Where(session => session.HallId == hallId);
        }
        // "Sessions for this speaker" — the Speakers grid deep-links here with
        // ?speakerId={id}. The session↔speaker link is the M-to-M SessionSpeaker
        // set, so match any session the speaker is linked to.
        if (query.Filters.TryGetValue("speakerId", out var speakerIdRaw)
            && Guid.TryParse(speakerIdRaw, out var speakerId))
        {
            rows = rows.Where(session => session.Speakers.Any(link => link.SpeakerId == speakerId));
        }

        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("code", true) => rows.OrderByDescending(session => session.Code),
            ("code", false) => rows.OrderBy(session => session.Code),
            ("title", true) => rows.OrderByDescending(session => session.Title),
            ("title", false) => rows.OrderBy(session => session.Title),
            ("endutc", true) => rows.OrderByDescending(session => session.EndUtc),
            ("endutc", false) => rows.OrderBy(session => session.EndUtc),
            _ => rows.OrderBy(session => session.StartUtc),
        };

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows
            .Skip(skip)
            .Take(top)
            .Select(session => new AdminSessionSummary(
                session.Id,
                session.Code,
                session.Title,
                session.TitleArabic,
                session.HallId,
                session.Hall!.Name,
                session.Hall!.NameArabic,
                session.StartUtc,
                session.EndUtc,
                session.CapacityOverride ?? session.Hall!.Capacity,
                session.IsActive,
                session.CreatedAt,
                session.CategoryId,
                session.Status,
                session.Type,
                // D-506 — carried so the grid Excel export round-trips them.
                session.Description,
                session.DescriptionArabic,
                session.LiveStreamUrl,
                session.LiveSignLanguageUrl,
                session.LiveCaptions,
                session.LiveCaptionsArabic,
                session.SeatSelectionModeOverride))
            .ToListAsync(cancellationToken);

        return GridPage<AdminSessionSummary>.Of(page, total,
            skip, top);
    }

    public async Task<AdminSessionDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var session = await dbContext.Sessions
            .AsNoTracking()
            .Include(row => row.Hall)
            .Include(row => row.Speakers).ThenInclude(speakerLink => speakerLink.Speaker)
            .Include(row => row.Themes)
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        return session is null ? null : ToDetail(session);
    }

    public async Task<AdminSessionDetail> CreateAsync(
        Guid actorUserId,
        AdminCreateSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var (code, title, titleArabic) = ValidateAndNormalise(
            request.Code, request.Title, request.TitleArabic);
        ValidateTimeWindow(request.StartUtc, request.EndUtc);
        ValidateCapacity(request.CapacityOverride);
        ValidateLiveUrls(request.LiveStreamUrl, request.LiveSignLanguageUrl);
        ValidateTextLengths(
            request.Description, request.DescriptionArabic,
            request.LiveCaptions, request.LiveCaptionsArabic,
            request.LiveStreamUrl, request.LiveSignLanguageUrl);

        var hall = await ResolveHallAsync(request.HallId, cancellationToken);
        await EnsureSpeakersExistAsync(request.Speakers, cancellationToken);
        await EnsureThemesExistAsync(request.ThemeIds, cancellationToken);
        await EnsureCategoryIsValidAsync(request.CategoryId, cancellationToken);

        // S-2 — reject a new session that overlaps another in the same hall.
        await EnsureNoHallTimeOverlapAsync(
            hall.Id, request.StartUtc, request.EndUtc, Guid.Empty, cancellationToken);

        var clash = await dbContext.Sessions
            .AsNoTracking()
            .AnyAsync(row => row.Code == code, cancellationToken);
        if (clash)
        {
            throw new ApiException(
                ErrorCodes.SessionCodeDuplicate, 409,
                $"A session with code '{code}' already exists.",
                $"توجد جلسة بالرمز '{code}' بالفعل.");
        }

        var now = timeProvider.GetUtcNow();
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = code,
            Title = title,
            TitleArabic = titleArabic,
            Description = NullIfBlank(request.Description),
            DescriptionArabic = NullIfBlank(request.DescriptionArabic),
            HallId = hall.Id,
            CategoryId = request.CategoryId,
            // D-452 — session type for the app's type tabs.
            Type = request.Type,
            // D-485 — optional per-session seat-selection-mode override.
            SeatSelectionModeOverride = request.SeatSelectionModeOverride,
            StartUtc = request.StartUtc,
            EndUtc = request.EndUtc,
            CapacityOverride = request.CapacityOverride,
            // §8 — live broadcast stream URLs (manual stub provider).
            LiveStreamUrl = NullIfBlank(request.LiveStreamUrl),
            LiveSignLanguageUrl = NullIfBlank(request.LiveSignLanguageUrl),
            // P5 — D-439: AI live-caption text (manual stub provider, bilingual).
            LiveCaptions = NullIfBlank(request.LiveCaptions),
            LiveCaptionsArabic = NullIfBlank(request.LiveCaptionsArabic),
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
        dbContext.Sessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SessionCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={session.Id}; code={code}; hall={hall.Id}",
        }, cancellationToken);

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
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.SessionNotFound, 404,
                "The session was not found.",
                "لم يتم العثور على الجلسة.");

        var (code, title, titleArabic) = ValidateAndNormalise(
            request.Code, request.Title, request.TitleArabic);
        ValidateTimeWindow(request.StartUtc, request.EndUtc);
        ValidateCapacity(request.CapacityOverride);
        ValidateLiveUrls(request.LiveStreamUrl, request.LiveSignLanguageUrl);
        ValidateTextLengths(
            request.Description, request.DescriptionArabic,
            request.LiveCaptions, request.LiveCaptionsArabic,
            request.LiveStreamUrl, request.LiveSignLanguageUrl);

        var hall = await ResolveHallAsync(request.HallId, cancellationToken);
        await EnsureSpeakersExistAsync(request.Speakers, cancellationToken);
        await EnsureThemesExistAsync(request.ThemeIds, cancellationToken);
        await EnsureCategoryIsValidAsync(request.CategoryId, cancellationToken);

        // S-1 (owner default) — capture whether the hall or the time window is
        // changing BEFORE the fields are overwritten; either invalidates held seats.
        var hallChanged = session.HallId != hall.Id;
        var timeChanged = session.StartUtc != request.StartUtc
            || session.EndUtc != request.EndUtc;

        // S-1 — soft-deleting via update (IsActive true -> false) must not orphan
        // active visitor bookings (same rule as DeactivateAsync).
        if (session.IsActive && !request.IsActive)
        {
            var heldVisitorBookings = await dbContext.SeatReservations
                .CountAsync(reservation =>
                    reservation.SessionId == id
                    && reservation.ReleasedAt == null
                    && reservation.ReservedForUserId != null,
                    cancellationToken);
            if (heldVisitorBookings > 0)
            {
                throw new ApiException(
                    ErrorCodes.SessionHasActiveBookings, 409,
                    $"This session has {heldVisitorBookings} active booking(s) — cancel or reject them before deactivating it.",
                    $"لهذه الجلسة {heldVisitorBookings} حجز نشط — يجب إلغاؤها أو رفضها قبل إلغاء تفعيلها.");
            }
        }

        // S-1 — never shrink the effective capacity below the seats already held
        // (visitor bookings + admin row-blocks), which would silently oversell.
        // Effective capacity = CapacityOverride ?? Hall.Capacity, so CLEARING the
        // override to null lowers it exactly like reducing it (#7 — a null override
        // must not bypass this guard); resolve the effective value and check that,
        // never just the non-null override. Skipped when the hall or time is
        // changing, because that path cascade-releases every held seat anyway
        // (verify correction).
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
            var clash = await dbContext.Sessions
                .AsNoTracking()
                .AnyAsync(row => row.Id != id && row.Code == code, cancellationToken);
            if (clash)
            {
                throw new ApiException(
                    ErrorCodes.SessionCodeDuplicate, 409,
                    $"A session with code '{code}' already exists.",
                    $"توجد جلسة بالرمز '{code}' بالفعل.");
            }
        }

        // S-2 — reject the update if the new hall/time overlaps another session.
        // Only checked when the slot actually moves (verify correction): a
        // title-only edit of a session with a pre-existing overlapping sibling
        // (legacy data) must stay saveable, and deactivation must not be blocked.
        if (hallChanged || timeChanged)
        {
            await EnsureNoHallTimeOverlapAsync(
                hall.Id, request.StartUtc, request.EndUtc, id, cancellationToken);
        }

        session.Code = code;
        session.Title = title;
        session.TitleArabic = titleArabic;
        session.Description = NullIfBlank(request.Description);
        session.DescriptionArabic = NullIfBlank(request.DescriptionArabic);
        session.HallId = hall.Id;
        session.CategoryId = request.CategoryId;
        session.Type = request.Type; // D-452
        session.SeatSelectionModeOverride = request.SeatSelectionModeOverride; // D-485
        session.StartUtc = request.StartUtc;
        session.EndUtc = request.EndUtc;
        session.CapacityOverride = request.CapacityOverride;
        // §8 — live broadcast stream URLs (manual stub provider).
        session.LiveStreamUrl = NullIfBlank(request.LiveStreamUrl);
        session.LiveSignLanguageUrl = NullIfBlank(request.LiveSignLanguageUrl);
        // P5 — D-439: AI live-caption text (manual stub provider, bilingual).
        session.LiveCaptions = NullIfBlank(request.LiveCaptions);
        session.LiveCaptionsArabic = NullIfBlank(request.LiveCaptionsArabic);
        session.IsActive = request.IsActive;
        session.UpdatedAt = timeProvider.GetUtcNow();

        ReplaceSpeakerLinks(session, request.Speakers);
        ReplaceThemeLinks(session, request.ThemeIds);

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

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SessionUpdated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={session.Id}; code={code}; active={session.IsActive}",
        }, cancellationToken);

        // S-1 — notify each affected visitor AFTER the commit (best-effort; the
        // dispatcher writes to the Identity DB via its own context, D-157).
        foreach (var reservation in releasedReservations)
        {
            await TryNotifyBookingReleasedAsync(reservation, session, cancellationToken);
        }

        return (await GetAsync(session.Id, cancellationToken))!;
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
                && reservation.ReservedForUserId != null,
                cancellationToken);
        if (activeBookings > 0)
        {
            throw new ApiException(
                ErrorCodes.SessionHasActiveBookings, 409,
                $"This session has {activeBookings} active booking(s) — cancel or reject them before deleting it.",
                $"لهذه الجلسة {activeBookings} حجز نشط — يجب إلغاؤها أو رفضها قبل حذفها.");
        }

        session.IsActive = false;
        session.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SessionDeactivated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={session.Id}; code={session.Code}",
        }, cancellationToken);
    }

    // P3.2 — D-231: the legal adjacent lifecycle moves. Any pair not listed
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
            return ToDetail(session); // idempotent — nothing changed
        }

        if (!AllowedTransitions.Contains((from, status)))
        {
            throw new ApiException(
                ErrorCodes.SessionStatusTransitionInvalid, 400,
                $"A session cannot move from {from} to {status}.",
                $"لا يمكن نقل الجلسة من {from} إلى {status}.");
        }

        var now = timeProvider.GetUtcNow();
        // S-7 — adjacency alone is not enough: a session cannot be marked Held
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
        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = eventType,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={session.Id}; code={session.Code}; {from}->{status}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} moved Session {Code} ({Id}) {From} -> {To}",
            actorUserId, session.Code, session.Id, from, status);

        return ToDetail(session);
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

        // D-568 (S7) — stream the bytes into the unified StoredFile store (Internal
        // tier, plaintext + seekable for Range streaming, owner = the session). The
        // store never buffers the whole video and computes the SHA-256 on the fly;
        // RecordingStoredFileName is repurposed as the bare-Guid pointer + "has
        // recording" sentinel. The malware scan is skipped (size-capped policy).
        var priorPointer = session.RecordingStoredFileName;
        var result = await fileService.CreateStreamedAsync(
            FileService.SessionRecording, session.Id, content, fileName, contentType,
            System.IO.Path.GetExtension(fileName), actorUserId, cancellationToken);

        var now = timeProvider.GetUtcNow();
        session.RecordingStoredFileName = result.Id.ToString();
        session.RecordingFileName = fileName;
        session.RecordingContentType = contentType;
        session.RecordingSizeBytes = result.SizeBytes;
        session.RecordingUploadedAt = now;
        session.RecordingUploadedByUserId = actorUserId;
        session.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        // Retire the prior recording (best-effort — the new one is the committed
        // source of truth; SessionRecording is DeletableDefault:true).
        await RetireRecordingAsync(priorPointer, result.Id, actorUserId, cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SessionRecordingUploaded,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={session.Id}; code={session.Code}; file={fileName}; bytes={sizeBytes}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} uploaded recording for Session {Code} ({Id}), {Bytes} bytes",
            actorUserId, session.Code, session.Id, sizeBytes);

        return ToDetail(session);
    }

    public async Task<AdminSessionDetail> DeleteRecordingAsync(
        Guid actorUserId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadFullAsync(id, cancellationToken);

        if (session.RecordingStoredFileName is null)
        {
            return ToDetail(session); // idempotent — nothing to delete
        }

        var storedFileName = session.RecordingStoredFileName;
        session.RecordingStoredFileName = null;
        session.RecordingFileName = null;
        session.RecordingContentType = null;
        session.RecordingSizeBytes = null;
        session.RecordingUploadedAt = null;
        session.RecordingUploadedByUserId = null;
        session.UpdatedAt = timeProvider.GetUtcNow();
        // Clear the metadata first, then drop the file: if the file delete
        // fails the app already sees "no recording" and only an orphan file
        // is left behind (harmless), never a row pointing at a missing file.
        await dbContext.SaveChangesAsync(cancellationToken);
        await RetireRecordingAsync(storedFileName, null, actorUserId, cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SessionRecordingDeleted,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={session.Id}; code={session.Code}",
        }, cancellationToken);

        return ToDetail(session);
    }

    /// <summary>D-568 (S7) — best-effort retirement of a recording's <c>StoredFile</c>
    /// (soft-delete + byte-unlink). The row is already updated (source of truth), so a
    /// delete failure must not fail the operation — worst case leaves one orphan blob.
    /// No-op when the pointer is absent/unparseable or is the just-uploaded file.</summary>
    private async Task RetireRecordingAsync(
        string? priorPointer, Guid? newFileId, Guid actorUserId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(priorPointer, out var old) || old == newFileId) { return; }
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

    // P3.2 — loads the session with the navigations ToDetail needs, tracked
    // (callers mutate it). Shared by SetStatus / Upload / Delete recording.
    private async Task<Session> LoadFullAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.Sessions
            .Include(row => row.Hall)
            .Include(row => row.Speakers).ThenInclude(link => link.Speaker)
            .Include(row => row.Themes)
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
        ?? throw new ApiException(
            ErrorCodes.SessionNotFound, 404,
            "The session was not found.",
            "لم يتم العثور على الجلسة.");

    private static (string code, string title, string titleArabic) ValidateAndNormalise(
        string codeRaw, string titleRaw, string titleArabicRaw)
    {
        var code = (codeRaw ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length is < 2 or > 16)
        {
            throw new ApiException(
                ErrorCodes.SessionInvalid, 400,
                "Session code must be between 2 and 16 characters.",
                "يجب أن يتراوح طول رمز الجلسة بين 2 و 16 حرفاً.");
        }
        var title = (titleRaw ?? string.Empty).Trim();
        if (title.Length is < 1 or > 256)
        {
            throw new ApiException(
                ErrorCodes.SessionInvalid, 400,
                "Session English title must be between 1 and 256 characters.",
                "يجب أن يتراوح طول العنوان الإنجليزي للجلسة بين 1 و 256 حرفاً.");
        }
        var titleArabic = (titleArabicRaw ?? string.Empty).Trim();
        if (titleArabic.Length is < 1 or > 256)
        {
            throw new ApiException(
                ErrorCodes.SessionInvalid, 400,
                "Session Arabic title must be between 1 and 256 characters.",
                "يجب أن يتراوح طول العنوان العربي للجلسة بين 1 و 256 حرفاً.");
        }
        return (code, title, titleArabic);
    }

    // S-7 — the clock + recording guards for a FORWARD lifecycle move. Reverse
    // (undo) moves (Held->Scheduled, Recorded->Held, Published->Recorded) carry no
    // guard. No admin override flag in this increment — an admin who must publish
    // pre-recorded content adjusts the session's start time / uploads a recording.
    private static void ValidateStatusGuards(
        Session session, SessionStatus target, DateTimeOffset now)
    {
        if (target == SessionStatus.Held && now < session.StartUtc)
        {
            throw new ApiException(
                ErrorCodes.SessionStatusGuardFailed, 400,
                "A session cannot be marked Held before it has started.",
                "لا يمكن تعيين الجلسة كمنعقدة قبل أن تبدأ.");
        }
        if (target is SessionStatus.Recorded or SessionStatus.Published
            && session.RecordingStoredFileName is null)
        {
            throw new ApiException(
                ErrorCodes.SessionStatusGuardFailed, 400,
                "A session needs an attached recording before it can be marked Recorded or Published.",
                "تحتاج الجلسة إلى تسجيل مرفق قبل تعيينها كمُسجّلة أو منشورة.");
        }
    }

    private static void ValidateTimeWindow(DateTimeOffset start, DateTimeOffset end)
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

    // §8 / D-349 — a non-blank live feed URL must be a YouTube link or a direct
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

    // §7 (validation triple-lock) — the free-text + live-feed fields carry EF
    // column caps (SessionConfiguration: Description / DescriptionArabic /
    // LiveCaptions / LiveCaptionsArabic = nvarchar(2048); LiveStreamUrl /
    // LiveSignLanguageUrl = nvarchar(1024)). Enforce those caps here so
    // over-length input returns a clean 400 instead of a SaveChanges
    // "string or binary data would be truncated" 500 (#19). Measured on the
    // trimmed value actually persisted by NullIfBlank, mirroring AdminHallService.
    private static void ValidateTextLengths(
        string? description, string? descriptionArabic,
        string? liveCaptions, string? liveCaptionsArabic,
        string? liveStreamUrl, string? liveSignLanguageUrl)
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

    // S-2 — two active sessions must not occupy the same hall at overlapping
    // times. Half-open overlap: existing.Start < newEnd AND newStart < existing.End.
    // Excludes the session being updated (excludeSessionId) and soft-deleted rows.
    private async Task EnsureNoHallTimeOverlapAsync(
        Guid hallId, DateTimeOffset startUtc, DateTimeOffset endUtc,
        Guid excludeSessionId, CancellationToken cancellationToken)
    {
        var overlaps = await dbContext.Sessions
            .AsNoTracking()
            .AnyAsync(other =>
                other.IsActive
                && other.HallId == hallId
                && other.Id != excludeSessionId
                && other.StartUtc < endUtc
                && startUtc < other.EndUtc,
                cancellationToken);
        if (overlaps)
        {
            throw new ApiException(
                ErrorCodes.SessionHallTimeOverlap, 409,
                "Another active session already uses this hall at an overlapping time.",
                "توجد جلسة نشطة أخرى تستخدم هذه القاعة في وقت متداخل.");
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

    // B9b — D-226: the session category, when set, must be an active row in the
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

    // S-1 (owner default) — tell each affected visitor their held seat was released
    // because the session was moved or rescheduled. Reuses NotificationKind.BookingRejected
    // (in-app warning, session deep-link); the authoritative bilingual text is set below.
    private async Task TryNotifyBookingReleasedAsync(
        SeatReservation reservation, Session session, CancellationToken cancellationToken)
    {
        if (reservation.ReservedForUserId is not { } userId)
        {
            return; // an admin row-block has no attendee to notify
        }
        try
        {
            await notifications.DispatchAsync(new NotificationRequest
            {
                UserId = userId,
                Kind = NotificationKind.BookingRejected,
                Title = "Seat reservation released",
                TitleArabic = "تم إلغاء حجز المقعد",
                Body = $"\"{session.Title}\" was rescheduled or moved to another hall, so your seat was released. Please book again.",
                BodyArabic = $"تم تغيير موعد أو قاعة جلسة \"{session.TitleArabic}\"، لذا تم إلغاء حجز مقعدك. يرجى الحجز من جديد.",
                Severity = NotificationSeverity.Warning,
                RelatedEntityType = "Session",
                RelatedEntityId = session.Id,
                SendEmail = false,
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

    private static AdminSessionDetail ToDetail(Session session)
    {
        var hallSeats = session.Hall?.Capacity ?? 0;
        var effective = session.CapacityOverride ?? hallSeats;
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
            session.StartUtc,
            session.EndUtc,
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
            session.RecordingStoredFileName is not null,
            session.RecordingFileName,
            session.RecordingSizeBytes,
            session.RecordingUploadedAt,
            session.LiveStreamUrl,
            session.LiveSignLanguageUrl,
            // P5 — D-439: AI live-caption text.
            session.LiveCaptions,
            session.LiveCaptionsArabic,
            // D-452 — session type for the app's type tabs.
            session.Type,
            // D-485 — per-session seat-selection-mode override (null = inherit).
            session.SeatSelectionModeOverride);
    }
}
