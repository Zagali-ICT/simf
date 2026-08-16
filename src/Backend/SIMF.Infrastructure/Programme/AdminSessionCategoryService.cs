// Tests: SIMF.Api.Tests/SessionCategoriesTests.cs
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Grids;
using SIMF.Contracts.Admin;
using SIMF.Domain.Auditing;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Common.Grids;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Programme;

/// <summary>
/// Admin CRUD over the dynamic <see cref="SessionCategory"/>
/// lookup. Built on <see cref="SimfAppDbContext"/>. Mirrors
/// <c>AdminOrganisationService</c>: bilingual (Name / NameArabic), soft-delete
/// (IsActive), in-service validation, one audit row per mutation. Ships empty;
/// the team seeds categories once the client confirms the list.
/// </summary>
internal sealed class AdminSessionCategoryService(
    SimfAppDbContext db,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<AdminSessionCategoryService> logger) : IAdminSessionCategoryService
{
    /// <summary>
    /// The grid contract for /admin/session-categories: one entry per key
    /// SessionCategoriesList.razor can send, as both its filter and its sort. The
    /// display-order key is "order", not "displayOrder" — that is the column key the
    /// page sends, and a key not declared here is a 400.
    /// </summary>
    private static readonly GridColumns<SessionCategory> Columns =
        new GridColumns<SessionCategory>()
            .Add("name", category => category.Name, searchable: true)
            .Add("nameArabic", category => category.NameArabic, searchable: true)
            .Add("order", category => category.DisplayOrder)
            .Add("isActive", category => category.IsActive)
            .DefaultOrder("order")
            .DefaultOrder("name")
            .PageSize(fallback: 25, max: 200);

    private static readonly
        Expression<Func<SessionCategory, AdminSessionCategorySummary>> ToSummary =
        category => new AdminSessionCategorySummary(
            category.Id,
            category.Name,
            category.NameArabic,
            category.DisplayOrder,
            category.IsActive);

    public Task<GridPage<AdminSessionCategorySummary>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default) =>
        db.SessionCategories.ToGridPageAsync(
            query, Columns, category => category.Id, ToSummary, cancellationToken);

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
        var (name, nameArabic) = ValidateAndNormalise(request.Name, request.NameArabic);
        await EnsureNameIsFreeAsync(name, null, cancellationToken);

        var now = timeProvider.SimfNow();
        var category = new SessionCategory
        {
            Id = Guid.NewGuid(),
            Name = name,
            NameArabic = nameArabic,
            DisplayOrder = request.DisplayOrder,
            IsActive = true,
            CreatedAt = now,
        };
        db.SessionCategories.Add(category);
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.SessionCategoryCreated,
            actorUserId,
            $"id={category.Id}; name={name}",
            cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} created SessionCategory {Name} ({Id})",
            actorUserId, name, category.Id);

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

        var (name, nameArabic) = ValidateAndNormalise(request.Name, request.NameArabic);

        // Read the CURRENT state before the assignments below overwrite it.
        var renamed = !string.Equals(category.Name, name, StringComparison.OrdinalIgnoreCase);
        if (request.IsActive && (renamed || !category.IsActive))
        {
            await EnsureNameIsFreeAsync(name, id, cancellationToken);
        }

        category.Name = name;
        category.NameArabic = nameArabic;
        category.DisplayOrder = request.DisplayOrder;
        category.IsActive = request.IsActive;
        category.UpdatedAt = timeProvider.SimfNow();
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.SessionCategoryUpdated,
            actorUserId,
            $"id={category.Id}; name={name}; active={category.IsActive}",
            cancellationToken);

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
        category.UpdatedAt = timeProvider.SimfNow();
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.SessionCategoryDeactivated,
            actorUserId,
            $"id={category.Id}; name={category.Name}",
            cancellationToken);
    }

    /// <summary>The unique index over <c>Name</c> is FILTERED to the active rows,
    /// so the name is contended only among them — which makes reactivation a clash
    /// path in its own right, with no rename involved. Every sibling lookup
    /// (AdminThemeService, AdminHallService) pre-checks for the same reason: without
    /// it the collision surfaces from SaveChanges as a raw DbUpdateException, and
    /// the caller gets a 500 where the answer is a 409.</summary>
    private async Task EnsureNameIsFreeAsync(
        string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        var clash = await db.SessionCategories
            .AsNoTracking()
            .AnyAsync(
                category => category.IsActive
                    && category.Name == name
                    && (excludeId == null || category.Id != excludeId),
                cancellationToken);
        if (!clash)
        {
            return;
        }
        throw new ApiException(
            ErrorCodes.SessionCategoryInvalid, 409,
            $"A session category named '{name}' already exists.",
            $"يوجد تصنيف جلسة بالاسم '{name}' بالفعل.");
    }

    private static (string Name, string NameArabic) ValidateAndNormalise(
        string nameRaw, string nameArabicRaw)
    {
        var name = (nameRaw ?? string.Empty).Trim();
        if (name.Length is < 1 or > 128)
        {
            throw new ApiException(
                ErrorCodes.SessionCategoryInvalid, 400,
                "Session category English name must be between 1 and 128 characters.",
                "يجب أن يتراوح طول الاسم الإنجليزي للتصنيف بين 1 و 128 حرفاً.");
        }
        var nameArabic = (nameArabicRaw ?? string.Empty).Trim();
        if (nameArabic.Length is < 1 or > 128)
        {
            throw new ApiException(
                ErrorCodes.SessionCategoryInvalid, 400,
                "Session category Arabic name must be between 1 and 128 characters.",
                "يجب أن يتراوح طول الاسم العربي للتصنيف بين 1 و 128 حرفاً.");
        }
        return (name, nameArabic);
    }

    private static ApiException NotFound() =>
        new(
            ErrorCodes.SessionCategoryNotFound, 404,
            "The session category was not found.",
            "لم يتم العثور على تصنيف الجلسة.");

    private static AdminSessionCategoryDetail ToDetail(SessionCategory c) => new(
        c.Id,
        c.Name,
        c.NameArabic,
        c.DisplayOrder,
        c.IsActive,
        c.CreatedAt,
        c.UpdatedAt);
}
