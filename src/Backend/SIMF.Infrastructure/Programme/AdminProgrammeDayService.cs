// Tests: SIMF.Api.Tests/ProgrammeDaysTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Domain.Auditing;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Programme;

/// <summary>
/// Admin CRUD over the <see cref="ProgrammeDay"/> rows ("تفاصيل اليوم").
/// Built on <see cref="SimfAppDbContext"/>; mirrors
/// <c>AdminSessionCategoryService</c> — bilingual title, soft-delete, in-service
/// validation, one audit row per mutation — plus a <c>Date</c>, a one-active-day
/// -per-date uniqueness guard, and a <c>HasImage</c> flag resolved from the
/// unified Asset table (the logo is the <c>ProgrammeDayImage</c> asset
/// owned by the day's Id — no logo column).
/// </summary>
internal sealed class AdminProgrammeDayService(
    SimfAppDbContext db,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<AdminProgrammeDayService> logger) : IAdminProgrammeDayService
{
    public async Task<GridPage<AdminProgrammeDaySummary>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var (skip, top) = query.ClampPage(25, 200);

        var rows = db.ProgrammeDays.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(day =>
                EF.Functions.Like(day.Title, $"%{term}%")
                || EF.Functions.Like(day.TitleArabic, $"%{term}%"));
        }

        // CP grid per-column filters. Unknown columns are ignored.
        foreach (var (column, raw) in query.Filters)
        {
            if (string.IsNullOrWhiteSpace(raw)) { continue; }
            var v = raw.Trim();
            switch (column.ToLowerInvariant())
            {
                case "title":
                    rows = rows.Where(day => day.Title.Contains(v));
                    break;
                case "titlearabic":
                    rows = rows.Where(day => day.TitleArabic.Contains(v));
                    break;
                case "isactive":
                    if (bool.TryParse(v, out var isActive))
                    {
                        rows = rows.Where(day => day.IsActive == isActive);
                    }
                    break;
            }
        }

        // CP grid sortable columns. Default: DisplayOrder, then Date.
        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("title", true) => rows.OrderByDescending(day => day.Title),
            ("title", false) => rows.OrderBy(day => day.Title),
            ("date", true) => rows.OrderByDescending(day => day.Date),
            ("date", false) => rows.OrderBy(day => day.Date),
            ("order", true) => rows.OrderByDescending(day => day.DisplayOrder),
            ("order", false) => rows.OrderBy(day => day.DisplayOrder),
            ("isactive", true) => rows.OrderByDescending(day => day.IsActive),
            ("isactive", false) => rows.OrderBy(day => day.IsActive),
            _ => rows.OrderBy(day => day.DisplayOrder).ThenBy(day => day.Date),
        };

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows
            .Skip(skip)
            .Take(top)
            .Select(day => new
            {
                day.Id,
                day.Date,
                day.Title,
                day.TitleArabic,
                day.DisplayOrder,
                day.IsActive,
            })
            .ToListAsync(cancellationToken);

        var withImage = await DaysWithImageAsync(
            page.Select(d => d.Id).ToList(), cancellationToken);

        var summaries = page
            .Select(d => new AdminProgrammeDaySummary(
                d.Id, d.Date, d.Title, d.TitleArabic, d.DisplayOrder,
                withImage.Contains(d.Id), d.IsActive))
            .ToList();

        return GridPage<AdminProgrammeDaySummary>.Of(summaries, total,
            skip, top);
    }

    public async Task<AdminProgrammeDayDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var day = await db.ProgrammeDays
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        if (day is null)
        {
            return null;
        }
        var hasImage = await HasImageAsync(id, cancellationToken);
        return ToDetail(day, hasImage);
    }

    public async Task<AdminProgrammeDayDetail> CreateAsync(
        Guid actorUserId,
        AdminCreateProgrammeDayRequest request,
        CancellationToken cancellationToken = default)
    {
        var (title, titleArabic) =
            ValidateAndNormalise(request.Title, request.TitleArabic);
        await EnsureUniqueDateAsync(request.Date, null, cancellationToken);

        var now = timeProvider.SimfNow();
        var day = new ProgrammeDay
        {
            Id = Guid.NewGuid(),
            Date = request.Date,
            Title = title,
            TitleArabic = titleArabic,
            DisplayOrder = request.DisplayOrder,
            IsActive = true,
            CreatedAt = now,
        };
        db.ProgrammeDays.Add(day);
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.ProgrammeDayCreated,
            actorUserId,
            $"id={day.Id}; date={day.Date:yyyy-MM-dd}; title={title}",
            cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} created ProgrammeDay {Title} ({Id}) on {Date}",
            actorUserId, title, day.Id, day.Date);

        return ToDetail(day, false);
    }

    public async Task<AdminProgrammeDayDetail> UpdateAsync(
        Guid actorUserId,
        Guid id,
        AdminUpdateProgrammeDayRequest request,
        CancellationToken cancellationToken = default)
    {
        var day = await db.ProgrammeDays
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw NotFound();

        var (title, titleArabic) =
            ValidateAndNormalise(request.Title, request.TitleArabic);
        await EnsureUniqueDateAsync(request.Date, id, cancellationToken);

        day.Date = request.Date;
        day.Title = title;
        day.TitleArabic = titleArabic;
        day.DisplayOrder = request.DisplayOrder;
        day.IsActive = request.IsActive;
        day.UpdatedAt = timeProvider.SimfNow();
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.ProgrammeDayUpdated,
            actorUserId,
            $"id={day.Id}; date={day.Date:yyyy-MM-dd}; active={day.IsActive}",
            cancellationToken);

        var hasImage = await HasImageAsync(id, cancellationToken);
        return ToDetail(day, hasImage);
    }

    public async Task DeactivateAsync(
        Guid actorUserId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var day = await db.ProgrammeDays
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw NotFound();

        if (!day.IsActive)
        {
            return; // idempotent
        }

        day.Deactivate();
        day.UpdatedAt = timeProvider.SimfNow();
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.ProgrammeDayDeactivated,
            actorUserId,
            $"id={day.Id}; date={day.Date:yyyy-MM-dd}",
            cancellationToken);
    }

    /// <summary>One active programme day per date — the invariant the
    /// public day-grouping (ListDaysAsync) relies on (otherwise two cards for
    /// one date pull the same sessions).</summary>
    private async Task EnsureUniqueDateAsync(
        DateOnly date, Guid? excludeId, CancellationToken cancellationToken)
    {
        var clash = await db.ProgrammeDays
            .AsNoTracking()
            .AnyAsync(
                d => d.IsActive && d.Date == date
                    && (excludeId == null || d.Id != excludeId),
                cancellationToken);
        if (clash)
        {
            throw new ApiException(
                ErrorCodes.ProgrammeDayInvalid, 400,
                "A programme day already exists for that date.",
                "يوجد يوم برنامج مسجّل بالفعل لهذا التاريخ.");
        }
    }

    private Task<bool> HasImageAsync(Guid dayId, CancellationToken cancellationToken) =>
        db.StoredFiles.AsNoTracking().AnyAsync(
            f => f.IsActive
                && f.Service == FileService.ProgrammeDayImage
                && f.OwnerEntityId == dayId,
            cancellationToken);

    private async Task<HashSet<Guid>> DaysWithImageAsync(
        IReadOnlyCollection<Guid> dayIds, CancellationToken cancellationToken)
    {
        if (dayIds.Count == 0)
        {
            return new HashSet<Guid>();
        }
        // The day image now lives in the unified StoredFile store.
        return (await db.StoredFiles
                .AsNoTracking()
                .Where(f => f.IsActive
                    && f.Service == FileService.ProgrammeDayImage
                    && f.OwnerEntityId != null
                    && dayIds.Contains(f.OwnerEntityId.Value))
                .Select(f => f.OwnerEntityId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet();
    }

    private static (string Title, string TitleArabic) ValidateAndNormalise(
        string titleRaw, string titleArabicRaw)
    {
        var title = (titleRaw ?? string.Empty).Trim();
        if (title.Length is < 1 or > 128)
        {
            throw new ApiException(
                ErrorCodes.ProgrammeDayInvalid, 400,
                "Programme-day English title must be between 1 and 128 characters.",
                "يجب أن يتراوح طول العنوان الإنجليزي لليوم بين 1 و 128 حرفاً.");
        }
        var titleArabic = (titleArabicRaw ?? string.Empty).Trim();
        if (titleArabic.Length is < 1 or > 128)
        {
            throw new ApiException(
                ErrorCodes.ProgrammeDayInvalid, 400,
                "Programme-day Arabic title must be between 1 and 128 characters.",
                "يجب أن يتراوح طول العنوان العربي لليوم بين 1 و 128 حرفاً.");
        }
        return (title, titleArabic);
    }

    private static ApiException NotFound() =>
        new(
            ErrorCodes.ProgrammeDayNotFound, 404,
            "The programme day was not found.",
            "لم يتم العثور على يوم البرنامج.");

    private static AdminProgrammeDayDetail ToDetail(ProgrammeDay d, bool hasImage) =>
        new(
            d.Id,
            d.Date,
            d.Title,
            d.TitleArabic,
            d.DisplayOrder,
            hasImage,
            d.IsActive,
            d.CreatedAt,
            d.UpdatedAt);
}
