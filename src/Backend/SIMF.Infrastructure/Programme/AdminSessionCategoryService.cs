// Tests: SIMF.Api.Tests/SessionCategoriesTests.cs
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
/// B9b — D-226: admin CRUD over the dynamic <see cref="SessionCategory"/>
/// lookup (FDS-004 §5.4). Built on <see cref="SimfAppDbContext"/>. Mirrors
/// <c>AdminOrganisationService</c>: bilingual (NameEn / NameAr), soft-delete
/// (IsActive), in-service validation, one audit row per mutation. Ships empty;
/// the team seeds categories once the client confirms the list (OI-2).
/// </summary>
internal sealed class AdminSessionCategoryService(
    SimfAppDbContext db,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<AdminSessionCategoryService> logger) : IAdminSessionCategoryService
{
    public async Task<GridPage<AdminSessionCategorySummary>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 25, 1, 200);

        var rows = db.SessionCategories.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(category =>
                EF.Functions.Like(category.NameEn, $"%{term}%")
                || EF.Functions.Like(category.NameAr, $"%{term}%"));
        }
        if (query.Filters.TryGetValue("isActive", out var activeFilter)
            && bool.TryParse(activeFilter, out var isActive))
        {
            rows = rows.Where(category => category.IsActive == isActive);
        }

        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("name", true) => rows.OrderByDescending(category => category.NameEn),
            ("name", false) => rows.OrderBy(category => category.NameEn),
            _ => rows.OrderBy(category => category.DisplayOrder).ThenBy(category => category.NameEn),
        };

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows
            .Skip(skip)
            .Take(top)
            .Select(category => new AdminSessionCategorySummary(
                category.Id,
                category.NameEn,
                category.NameAr,
                category.DisplayOrder,
                category.IsActive))
            .ToListAsync(cancellationToken);

        return GridPage<AdminSessionCategorySummary>.Of(page, total,
            new GridQuery { Skip = skip, Top = top });
    }

    public async Task<AdminSessionCategoryDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var category = await db.SessionCategories
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        return category is null ? null : ToDetail(category);
    }

    public async Task<AdminSessionCategoryDetail> CreateAsync(
        Guid actorUserId,
        AdminCreateSessionCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var (nameEn, nameAr) = ValidateAndNormalise(request.NameEn, request.NameAr);

        var now = timeProvider.GetUtcNow();
        var category = new SessionCategory
        {
            Id = Guid.NewGuid(),
            NameEn = nameEn,
            NameAr = nameAr,
            DisplayOrder = request.DisplayOrder,
            IsActive = true,
            CreatedAt = now,
        };
        db.SessionCategories.Add(category);
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SessionCategoryCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={category.Id}; nameEn={nameEn}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} created SessionCategory {NameEn} ({Id})",
            actorUserId, nameEn, category.Id);

        return ToDetail(category);
    }

    public async Task<AdminSessionCategoryDetail> UpdateAsync(
        Guid actorUserId,
        Guid id,
        AdminUpdateSessionCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = await db.SessionCategories
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw NotFound();

        var (nameEn, nameAr) = ValidateAndNormalise(request.NameEn, request.NameAr);

        category.NameEn = nameEn;
        category.NameAr = nameAr;
        category.DisplayOrder = request.DisplayOrder;
        category.IsActive = request.IsActive;
        category.UpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SessionCategoryUpdated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={category.Id}; nameEn={nameEn}; active={category.IsActive}",
        }, cancellationToken);

        return ToDetail(category);
    }

    public async Task DeactivateAsync(
        Guid actorUserId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var category = await db.SessionCategories
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw NotFound();

        if (!category.IsActive)
        {
            return; // idempotent
        }

        category.Deactivate();
        category.UpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SessionCategoryDeactivated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={category.Id}; nameEn={category.NameEn}",
        }, cancellationToken);
    }

    private static (string NameEn, string NameAr) ValidateAndNormalise(
        string nameEnRaw, string nameArRaw)
    {
        var nameEn = (nameEnRaw ?? string.Empty).Trim();
        if (nameEn.Length is < 1 or > 128)
        {
            throw new ApiException(
                ErrorCodes.SessionCategoryInvalid, 400,
                "Session category English name must be between 1 and 128 characters.",
                "يجب أن يتراوح طول الاسم الإنجليزي للتصنيف بين 1 و 128 حرفاً.");
        }
        var nameAr = (nameArRaw ?? string.Empty).Trim();
        if (nameAr.Length is < 1 or > 128)
        {
            throw new ApiException(
                ErrorCodes.SessionCategoryInvalid, 400,
                "Session category Arabic name must be between 1 and 128 characters.",
                "يجب أن يتراوح طول الاسم العربي للتصنيف بين 1 و 128 حرفاً.");
        }
        return (nameEn, nameAr);
    }

    private static ApiException NotFound() =>
        new(
            ErrorCodes.SessionCategoryNotFound, 404,
            "The session category was not found.",
            "لم يتم العثور على تصنيف الجلسة.");

    private static AdminSessionCategoryDetail ToDetail(SessionCategory c) => new(
        c.Id,
        c.NameEn,
        c.NameAr,
        c.DisplayOrder,
        c.IsActive,
        c.CreatedAt,
        c.UpdatedAt);
}
