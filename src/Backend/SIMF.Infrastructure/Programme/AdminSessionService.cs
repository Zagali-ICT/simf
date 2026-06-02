// Tests: SIMF.Api.Tests/AdminSessionsTests.cs
// Tests: SIMF.Api.Tests/SessionLifecycleTests.cs (P3.2a — D-231 lifecycle)
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
                session.Status))
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
            StartUtc = request.StartUtc,
            EndUtc = request.EndUtc,
            CapacityOverride = request.CapacityOverride,
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
        session.StartUtc = request.StartUtc;
        session.EndUtc = request.EndUtc;
        session.CapacityOverride = request.CapacityOverride;
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
        // Load the full graph once, tracked (we mutate it), with the same
        // includes ToDetail needs — so the DTO is built in memory after the save
        // with no second round-trip. Create/Update re-fetch via GetAsync because
        // they deliberately don't load these navigations; SetStatus loads them
        // up front and skips the re-fetch.
        var session = await dbContext.Sessions
            .Include(row => row.Hall)
            .Include(row => row.Speakers).ThenInclude(link => link.Speaker)
            .Include(row => row.Themes)
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.SessionNotFound, 404,
                "The session was not found.",
                "لم يتم العثور على الجلسة.");

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

    // -- helpers --------------------------------------------------------------

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
            session.PublishedAt);
    }
}
