// Tests: SIMF.Api.Tests/SponsorsTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Auditing;
using SIMF.Application.Sponsors.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Domain.Sponsors;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Sponsors;

/// <summary>D-199 (Mockup page 23) — admin CRUD over <see cref="Sponsor"/>.
/// Mirrors AdminDelegationService structure (validation → mutate → save →
/// audit). Soft-delete via <see cref="Sponsor.Deactivate"/>.</summary>
internal sealed class AdminSponsorService(
    SimfAppDbContext appDbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider) : IAdminSponsorService
{
    public async Task<GridPage<AdminSponsorSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 25, 1, 200);

        var rows = appDbContext.Sponsors.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(sponsor =>
                EF.Functions.Like(sponsor.NameEn, $"%{term}%")
                || EF.Functions.Like(sponsor.NameAr, $"%{term}%"));
        }

        if (query.Filters.TryGetValue("isActive", out var activeFilter)
            && bool.TryParse(activeFilter, out var isActive))
        {
            rows = rows.Where(sponsor => sponsor.IsActive == isActive);
        }

        if (query.Filters.TryGetValue("tier", out var tierFilter)
            && int.TryParse(tierFilter, out var tierValue)
            && Enum.IsDefined(typeof(SponsorTier), tierValue))
        {
            var tier = (SponsorTier)tierValue;
            rows = rows.Where(sponsor => sponsor.Tier == tier);
        }

        // CP grid per-column text filters (D-255). The grid sends the column
        // Key as the filter key; unknown columns are ignored. The isActive /
        // tier filters above stay for API callers that pass the structured keys.
        foreach (var (column, raw) in query.Filters)
        {
            if (string.IsNullOrWhiteSpace(raw)) { continue; }
            var v = raw.Trim();
            switch (column.ToLowerInvariant())
            {
                case "nameen":
                    rows = rows.Where(sponsor => sponsor.NameEn.Contains(v));
                    break;
                case "namear":
                    rows = rows.Where(sponsor => sponsor.NameAr.Contains(v));
                    break;
            }
        }

        // CP grid sortable columns (D-255). Default (and any unknown sort):
        // Tier, then DisplayOrder, then NameAr — the public ordering.
        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("nameen", false) => rows.OrderBy(sponsor => sponsor.NameEn),
            ("nameen", true) => rows.OrderByDescending(sponsor => sponsor.NameEn),
            ("namear", false) => rows.OrderBy(sponsor => sponsor.NameAr),
            ("namear", true) => rows.OrderByDescending(sponsor => sponsor.NameAr),
            ("tier", false) => rows.OrderBy(sponsor => sponsor.Tier),
            ("tier", true) => rows.OrderByDescending(sponsor => sponsor.Tier),
            ("displayorder", false) => rows.OrderBy(sponsor => sponsor.DisplayOrder),
            ("displayorder", true) => rows.OrderByDescending(sponsor => sponsor.DisplayOrder),
            ("isactive", false) => rows.OrderBy(sponsor => sponsor.IsActive),
            ("isactive", true) => rows.OrderByDescending(sponsor => sponsor.IsActive),
            _ => rows
                .OrderBy(sponsor => sponsor.Tier)
                .ThenBy(sponsor => sponsor.DisplayOrder)
                .ThenBy(sponsor => sponsor.NameAr),
        };

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows
            .Skip(skip).Take(top)
            .Select(sponsor => new AdminSponsorSummary(
                sponsor.Id,
                sponsor.NameEn,
                sponsor.NameAr,
                (int)sponsor.Tier,
                sponsor.Tier.ToString(),
                sponsor.LogoRelativePath,
                sponsor.Url,
                sponsor.DisplayOrder,
                sponsor.IsActive,
                sponsor.CreatedAt))
            .ToListAsync(cancellationToken);

        return GridPage<AdminSponsorSummary>.Of(page, total,
            new GridQuery { Skip = skip, Top = top });
    }

    public async Task<AdminSponsorDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await appDbContext.Sponsors.AsNoTracking()
            .Where(sponsor => sponsor.Id == id)
            .Select(sponsor => ToDetail(sponsor))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<AdminSponsorDetail> CreateAsync(
        Guid actorUserId, AdminCreateSponsorRequest request,
        CancellationToken cancellationToken = default)
    {
        var (nameEn, nameAr, tier, logoRelativePath, url, displayOrder) =
            Validate(request.NameEn, request.NameAr, request.Tier,
                request.LogoRelativePath, request.Url, request.DisplayOrder);

        // Duplicate guard: an active sponsor with the same Arabic name in the
        // same tier is treated as a clash (matches the Country code-clash 409
        // pattern). Inactive rows do not block re-create.
        var clash = await appDbContext.Sponsors.AsNoTracking().AnyAsync(
            sponsor => sponsor.IsActive
                && sponsor.Tier == tier
                && sponsor.NameAr == nameAr,
            cancellationToken);
        if (clash)
        {
            throw new ApiException(ErrorCodes.SponsorDuplicate, 409,
                $"An active sponsor named '{nameAr}' already exists in this tier.",
                $"يوجد راعٍ نشط بالاسم '{nameAr}' في هذه الفئة بالفعل.");
        }

        var now = timeProvider.GetUtcNow();
        var sponsor = new Sponsor
        {
            Id = Guid.NewGuid(),
            NameEn = nameEn,
            NameAr = nameAr,
            Tier = tier,
            LogoRelativePath = logoRelativePath,
            Url = url,
            DisplayOrder = displayOrder,
            IsActive = true,
            CreatedAt = now,
        };

        appDbContext.Sponsors.Add(sponsor);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SponsorCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"sponsorId={sponsor.Id}; tier={tier}; nameAr={nameAr}",
        }, cancellationToken);

        return ToDetail(sponsor);
    }

    public async Task<AdminSponsorDetail> UpdateAsync(
        Guid actorUserId, Guid id, AdminUpdateSponsorRequest request,
        CancellationToken cancellationToken = default)
    {
        var (nameEn, nameAr, tier, logoRelativePath, url, displayOrder) =
            Validate(request.NameEn, request.NameAr, request.Tier,
                request.LogoRelativePath, request.Url, request.DisplayOrder);

        var sponsor = await appDbContext.Sponsors
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new ApiException(ErrorCodes.SponsorNotFound, 404,
                "The sponsor was not found.",
                "لم يتم العثور على الراعي.");

        var renamedOrRetiered =
            !string.Equals(sponsor.NameAr, nameAr, StringComparison.Ordinal)
            || sponsor.Tier != tier;
        if (request.IsActive && renamedOrRetiered)
        {
            var clash = await appDbContext.Sponsors.AsNoTracking().AnyAsync(
                s => s.Id != id
                    && s.IsActive
                    && s.Tier == tier
                    && s.NameAr == nameAr,
                cancellationToken);
            if (clash)
            {
                throw new ApiException(ErrorCodes.SponsorDuplicate, 409,
                    $"An active sponsor named '{nameAr}' already exists in this tier.",
                    $"يوجد راعٍ نشط بالاسم '{nameAr}' في هذه الفئة بالفعل.");
            }
        }

        sponsor.NameEn = nameEn;
        sponsor.NameAr = nameAr;
        sponsor.Tier = tier;
        sponsor.LogoRelativePath = logoRelativePath;
        sponsor.Url = url;
        sponsor.DisplayOrder = displayOrder;
        sponsor.IsActive = request.IsActive;
        sponsor.UpdatedAt = timeProvider.GetUtcNow();

        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SponsorUpdated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"sponsorId={sponsor.Id}; tier={tier}; active={sponsor.IsActive}",
        }, cancellationToken);

        return ToDetail(sponsor);
    }

    public async Task DeactivateAsync(
        Guid actorUserId, Guid id,
        CancellationToken cancellationToken = default)
    {
        var sponsor = await appDbContext.Sponsors
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new ApiException(ErrorCodes.SponsorNotFound, 404,
                "The sponsor was not found.",
                "لم يتم العثور على الراعي.");

        if (!sponsor.IsActive) { return; }

        sponsor.Deactivate();
        sponsor.UpdatedAt = timeProvider.GetUtcNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SponsorDeactivated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"sponsorId={sponsor.Id}",
        }, cancellationToken);
    }

    private static (string nameEn, string nameAr, SponsorTier tier,
        string? logoRelativePath, string? url, int displayOrder) Validate(
            string nameEnRaw, string nameArRaw, int tierRaw,
            string? logoRelativePathRaw, string? urlRaw, int displayOrderRaw)
    {
        var nameEn = (nameEnRaw ?? string.Empty).Trim();
        if (nameEn.Length is < 1 or > 256)
        {
            throw new ApiException(ErrorCodes.SponsorInvalid, 400,
                "Sponsor English name must be between 1 and 256 characters.",
                "يجب أن يتراوح الاسم الإنجليزي للراعي بين 1 و 256 حرفاً.");
        }

        var nameAr = (nameArRaw ?? string.Empty).Trim();
        if (nameAr.Length is < 1 or > 256)
        {
            throw new ApiException(ErrorCodes.SponsorInvalid, 400,
                "Sponsor Arabic name must be between 1 and 256 characters.",
                "يجب أن يتراوح الاسم العربي للراعي بين 1 و 256 حرفاً.");
        }

        if (!Enum.IsDefined(typeof(SponsorTier), tierRaw))
        {
            throw new ApiException(ErrorCodes.SponsorInvalid, 400,
                "Sponsor tier is not a recognised value.",
                "فئة الراعي ليست قيمة معروفة.");
        }
        var tier = (SponsorTier)tierRaw;

        var logoRelativePath = string.IsNullOrWhiteSpace(logoRelativePathRaw)
            ? null : logoRelativePathRaw.Trim();
        if (logoRelativePath is { Length: > 256 })
        {
            throw new ApiException(ErrorCodes.SponsorInvalid, 400,
                "Logo path must be 256 characters or fewer.",
                "يجب أن يكون مسار الشعار 256 حرفاً أو أقل.");
        }

        var url = string.IsNullOrWhiteSpace(urlRaw) ? null : urlRaw.Trim();
        if (url is { Length: > 512 })
        {
            throw new ApiException(ErrorCodes.SponsorInvalid, 400,
                "URL must be 512 characters or fewer.",
                "يجب أن يكون الرابط 512 حرفاً أو أقل.");
        }

        if (displayOrderRaw < 0)
        {
            throw new ApiException(ErrorCodes.SponsorInvalid, 400,
                "Display order must be zero or a positive integer.",
                "يجب أن يكون ترتيب العرض صفراً أو عدداً صحيحاً موجباً.");
        }

        return (nameEn, nameAr, tier, logoRelativePath, url, displayOrderRaw);
    }

    private static AdminSponsorDetail ToDetail(Sponsor sponsor) =>
        new(sponsor.Id,
            sponsor.NameEn,
            sponsor.NameAr,
            (int)sponsor.Tier,
            sponsor.Tier.ToString(),
            sponsor.LogoRelativePath,
            sponsor.Url,
            sponsor.DisplayOrder,
            sponsor.IsActive,
            sponsor.CreatedAt,
            sponsor.UpdatedAt);
}
