// Tests: SIMF.Api.Tests/InterestTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.UserProfile;
using SIMF.Domain.Auditing;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;

using SIMF.Common.Enums;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// Interests CRUD (P9 — D-050; الاهتمامات). The admin grid is paged +
/// filtered through <see cref="GridQuery"/>; mutations audit one row
/// each. The visitor picker (<see cref="ListActiveAsync"/>) hits the
/// composite filter index <c>(IsActive, DisplayOrder)</c>.
/// </summary>
internal sealed class InterestService(
    SimfIdentityDbContext dbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<InterestService> logger) : IInterestService
{
    public async Task<IReadOnlyList<InterestDto>> ListActiveAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Interests
            .AsNoTracking()
            .Where(interest => interest.IsActive)
            .OrderBy(interest => interest.DisplayOrder)
            .ThenBy(interest => interest.Name)
            .Select(interest => new InterestDto(
                interest.Id, interest.Name, interest.NameArabic, interest.DisplayOrder))
            .ToListAsync(cancellationToken);
    }

    public async Task<GridPage<AdminInterestSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 25, 1, 200);

        var rows = dbContext.Interests.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(interest =>
                EF.Functions.Like(interest.Name, $"%{term}%")
                || EF.Functions.Like(interest.NameArabic, $"%{term}%"));
        }

        if (query.Filters.TryGetValue("name", out var nameFilter)
            && !string.IsNullOrWhiteSpace(nameFilter))
        {
            rows = rows.Where(interest =>
                EF.Functions.Like(interest.Name, $"%{nameFilter}%"));
        }
        if (query.Filters.TryGetValue("nameArabic", out var arFilter)
            && !string.IsNullOrWhiteSpace(arFilter))
        {
            rows = rows.Where(interest =>
                EF.Functions.Like(interest.NameArabic, $"%{arFilter}%"));
        }
        if (query.Filters.TryGetValue("isActive", out var activeFilter)
            && bool.TryParse(activeFilter, out var isActive))
        {
            rows = rows.Where(interest => interest.IsActive == isActive);
        }

        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("name", true) => rows.OrderByDescending(interest => interest.Name),
            ("name", false) => rows.OrderBy(interest => interest.Name),
            ("namearabic", true) => rows.OrderByDescending(interest => interest.NameArabic),
            ("namearabic", false) => rows.OrderBy(interest => interest.NameArabic),
            ("displayorder", true) => rows.OrderByDescending(interest => interest.DisplayOrder),
            ("displayorder", false) => rows.OrderBy(interest => interest.DisplayOrder),
            ("createdat", false) => rows.OrderBy(interest => interest.CreatedAt),
            // Natural order matches the visitor picker — DisplayOrder, then Name.
            _ => rows.OrderBy(interest => interest.DisplayOrder).ThenBy(interest => interest.Name),
        };

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows
            .Skip(skip)
            .Take(top)
            .Select(interest => new AdminInterestSummary(
                interest.Id,
                interest.Name,
                interest.NameArabic,
                interest.DisplayOrder,
                interest.IsActive,
                interest.CreatedAt))
            .ToListAsync(cancellationToken);

        return GridPage<AdminInterestSummary>.Of(page, total,
            new GridQuery { Skip = skip, Top = top });
    }

    public async Task<AdminInterestSummary?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var interest = await dbContext.Interests
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        return interest is null
            ? null
            : new AdminInterestSummary(
                interest.Id, interest.Name, interest.NameArabic,
                interest.DisplayOrder, interest.IsActive, interest.CreatedAt);
    }

    public async Task<AdminInterestSummary> CreateAsync(
        Guid actorUserId,
        AdminCreateInterestRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.Interests
            .AsNoTracking()
            .AnyAsync(row => row.Name == request.Name, cancellationToken);
        if (existing)
        {
            throw new ApiException(
                ErrorCodes.InterestNameDuplicate, 409,
                $"An interest named '{request.Name}' already exists.",
                $"يوجد اهتمام بالاسم '{request.Name}' بالفعل.");
        }

        var now = timeProvider.GetUtcNow();
        var interest = new Interest
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            NameArabic = request.NameArabic.Trim(),
            DisplayOrder = request.DisplayOrder,
            IsActive = true,
            CreatedAt = now,
        };
        dbContext.Interests.Add(interest);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.InterestCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={interest.Id}; name={interest.Name}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} created interest {Name} ({Id})",
            actorUserId, interest.Name, interest.Id);

        return new AdminInterestSummary(
            interest.Id, interest.Name, interest.NameArabic,
            interest.DisplayOrder, interest.IsActive, interest.CreatedAt);
    }

    public async Task<AdminInterestSummary> UpdateAsync(
        Guid actorUserId,
        Guid id,
        AdminUpdateInterestRequest request,
        CancellationToken cancellationToken = default)
    {
        var interest = await dbContext.Interests
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.InterestNotFound, 404,
                "The interest was not found.",
                "لم يتم العثور على الاهتمام.");

        // Reject a rename that collides with another row.
        if (!string.Equals(interest.Name, request.Name, StringComparison.Ordinal))
        {
            var clash = await dbContext.Interests
                .AsNoTracking()
                .AnyAsync(
                    row => row.Id != id && row.Name == request.Name,
                    cancellationToken);
            if (clash)
            {
                throw new ApiException(
                    ErrorCodes.InterestNameDuplicate, 409,
                    $"An interest named '{request.Name}' already exists.",
                    $"يوجد اهتمام بالاسم '{request.Name}' بالفعل.");
            }
        }

        interest.Name = request.Name.Trim();
        interest.NameArabic = request.NameArabic.Trim();
        interest.DisplayOrder = request.DisplayOrder;
        interest.IsActive = request.IsActive;
        interest.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.InterestUpdated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={interest.Id}; name={interest.Name}; active={interest.IsActive}",
        }, cancellationToken);

        return new AdminInterestSummary(
            interest.Id, interest.Name, interest.NameArabic,
            interest.DisplayOrder, interest.IsActive, interest.CreatedAt);
    }

    public async Task DeactivateAsync(
        Guid actorUserId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var interest = await dbContext.Interests
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.InterestNotFound, 404,
                "The interest was not found.",
                "لم يتم العثور على الاهتمام.");

        if (!interest.IsActive)
        {
            // Idempotent — already deactivated.
            return;
        }

        interest.IsActive = false;
        interest.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.InterestDeactivated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={interest.Id}; name={interest.Name}",
        }, cancellationToken);
    }
}
