// Tests: SIMF.Api.Tests/AdminSessionsTests.cs (+ D-349 live-URL validation)
// Tests: SIMF.Api.Tests/SessionLifecycleTests.cs (P3.2a — D-231 lifecycle)
// Tests: SIMF.Api.Tests/SessionRecordingTests.cs (P3.2b — D-232 recording)
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Domain.Programme;
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
    ISessionRecordingStorage recordingStorage,
    ILogger<AdminSessionService> logger) : IAdminSessionService
{
    public async Task<GridPage<AdminSessionSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 25, 1, 200);

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
            new GridQuery { Skip = skip, Top = top });
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

        var hall = await ResolveHallAsync(request.HallId, cancellationToken);
        await EnsureSpeakersExistAsync(request.Speakers, cancellationToken);
        await EnsureThemesExistAsync(request.ThemeIds, cancellationToken);
        await EnsureCategoryIsValidAsync(request.CategoryId, cancellationToken);

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

        var hall = await ResolveHallAsync(request.HallId, cancellationToken);
        await EnsureSpeakersExistAsync(request.Speakers, cancellationToken);
        await EnsureThemesExistAsync(request.ThemeIds, cancellationToken);
        await EnsureCategoryIsValidAsync(request.CategoryId, cancellationToken);

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

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SessionUpdated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={session.Id}; code={code}; active={session.IsActive}",
        }, cancellationToken);

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

        // Stream the bytes to disk (the storage never buffers a whole-file
        // byte[]); persist only the metadata on the row.
        var storedFileName = await recordingStorage.SaveAsync(
            session.Id, content, fileName, cancellationToken);

        var now = timeProvider.GetUtcNow();
        session.RecordingStoredFileName = storedFileName;
        session.RecordingFileName = fileName;
        session.RecordingContentType = contentType;
        session.RecordingSizeBytes = sizeBytes;
        session.RecordingUploadedAt = now;
        session.RecordingUploadedByUserId = actorUserId;
        session.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

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
        await recordingStorage.DeleteAsync(storedFileName, cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SessionRecordingDeleted,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={session.Id}; code={session.Code}",
        }, cancellationToken);

        return ToDetail(session);
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
