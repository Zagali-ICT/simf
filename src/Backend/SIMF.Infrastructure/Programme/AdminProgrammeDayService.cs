// Tests: SIMF.Api.Tests/ProgrammeDaysTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Grids;
using SIMF.Contracts.Admin;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Common.Grids;
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
    /// <summary>
    /// The grid contract for /admin/programme-days: one entry per key
    /// ProgrammeDaysList.razor can send, as both its filter and its sort. The
    /// display-order key is "order", not "displayOrder" — that is the column key the
    /// page sends, and a key not declared here is a 400.
    /// </summary>
    private static readonly GridColumns<ProgrammeDay> Columns =
        new GridColumns<ProgrammeDay>()
            .Add("date", day => day.Date)
            .Add("title", day => day.Title, searchable: true)
            .Add("titleArabic", day => day.TitleArabic, searchable: true)
            .Add("order", day => day.DisplayOrder)
            .Add("isActive", day => day.IsActive)
            .DefaultOrder("order")
            .DefaultOrder("date")
            .PageSize(fallback: 25, max: 200);

    /// <summary>The image flag is resolved inside the projection rather than by a
    /// second pass over the page's ids: the day image is a row in the same
    /// <see cref="SimfAppDbContext"/>, so an EXISTS beside the SELECT answers it in
    /// the round trip that already reads the page.</summary>
    public Task<GridPage<AdminProgrammeDaySummary>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default) =>
        db.ProgrammeDays.ToGridPageAsync(
            query, Columns, day => day.Id,
            day => new AdminProgrammeDaySummary(
                day.Id, day.Date, day.Title, day.TitleArabic, day.DisplayOrder,
                db.StoredFiles.Any(file => file.IsActive
                    && file.Service == FileService.ProgrammeDayImage
                    && file.OwnerEntityId == day.Id),
                day.IsActive),
            cancellationToken);

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
        EnsureDisplayOrderIsValid(request.DisplayOrder);
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
        EnsureDisplayOrderIsValid(request.DisplayOrder);
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

    /// <summary>DisplayOrder is the CP grid's sort key and its default ordering, so
    /// a negative value silently pins the row ahead of every legitimate day. Rejected
    /// on both write paths, as the siblings over this shape (AdminThemeService,
    /// AdminSponsorService) do.</summary>
    private static void EnsureDisplayOrderIsValid(int displayOrder)
    {
        if (displayOrder < 0)
        {
            throw new ApiException(
                ErrorCodes.ProgrammeDayInvalid, 400,
                "Display order must be zero or a positive integer.",
                "يجب أن يكون ترتيب العرض صفراً أو عدداً صحيحاً موجباً.");
        }
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
                day => day.IsActive && day.Date == date
                    && (excludeId == null || day.Id != excludeId),
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
            file => file.IsActive
                && file.Service == FileService.ProgrammeDayImage
                && file.OwnerEntityId == dayId,
            cancellationToken);

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

    private static AdminProgrammeDayDetail ToDetail(ProgrammeDay day, bool hasImage) =>
        new(
            day.Id,
            day.Date,
            day.Title,
            day.TitleArabic,
            day.DisplayOrder,
            hasImage,
            day.IsActive,
            day.CreatedAt,
            day.UpdatedAt);
}
