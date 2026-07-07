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
/// <c>AdminOrganisationService</c>: bilingual (Name / NameArabic), soft-delete
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
        var (skip, top) = query.ClampPage(25, 200);

        var rows = db.SessionCategories.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(category =>
                EF.Functions.Like(category.Name, $"%{term}%")
                || EF.Functions.Like(category.NameArabic, $"%{term}%"));
        }

        // CP grid per-column filters (D-255). Unknown columns are ignored.
        foreach (var (column, raw) in query.Filters)
        {
            if (string.IsNullOrWhiteSpace(raw)) { continue; }
            var v = raw.Trim();
            switch (column.ToLowerInvariant())
            {
                case "name":
                    rows = rows.Where(category => category.Name.Contains(v));
                    break;
                case "namearabic":
                    rows = rows.Where(category => category.NameArabic.Contains(v));
                    break;
                case "isactive":
                    if (bool.TryParse(v, out var isActive))
                    {
                        rows = rows.Where(category => category.IsActive == isActive);
                    }
                    break;
            }
        }

        // CP grid sortable columns (D-255). Default: DisplayOrder, then Name.
        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("name", true) => rows.OrderByDescending(category => category.Name),
            ("name", false) => rows.OrderBy(category => category.Name),
            ("namearabic", true) => rows.OrderByDescending(category => category.NameArabic),
            ("namearabic", false) => rows.OrderBy(category => category.NameArabic),
            ("order", true) => rows.OrderByDescending(category => category.DisplayOrder),
            ("order", false) => rows.OrderBy(category => category.DisplayOrder),
            ("isactive", true) => rows.OrderByDescending(category => category.IsActive),
            ("isactive", false) => rows.OrderBy(category => category.IsActive),
            _ => rows.OrderBy(category => category.DisplayOrder).ThenBy(category => category.Name),
        };

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows
            .Skip(skip)
            .Take(top)
            .Select(category => new AdminSessionCategorySummary(
                category.Id,
                category.Name,
                category.NameArabic,
                category.DisplayOrder,
                category.IsActive))
            .ToListAsync(cancellationToken);

        return GridPage<AdminSessionCategorySummary>.Of(page, total,
            skip, top);
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
        var (name, nameArabic) = ValidateAndNormalise(request.Name, request.NameArabic);

        var now = timeProvider.GetUtcNow();
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

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SessionCategoryCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={category.Id}; name={name}",
        }, cancellationToken);

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

        category.Name = name;
        category.NameArabic = nameArabic;
        category.DisplayOrder = request.DisplayOrder;
        category.IsActive = request.IsActive;
        category.UpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SessionCategoryUpdated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={category.Id}; name={name}; active={category.IsActive}",
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
            Detail = $"id={category.Id}; name={category.Name}",
        }, cancellationToken);
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
