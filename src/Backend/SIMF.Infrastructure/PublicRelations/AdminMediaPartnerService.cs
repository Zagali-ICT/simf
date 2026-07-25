// Tests: SIMF.Api.Tests/AdminMediaPartnersTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Assets.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.PublicRelations.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.PublicRelations;
using SIMF.Domain.PublicRelations;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.PublicRelations;

/// <summary>D-199 (Mockup page 31) — admin CRUD over
/// <see cref="MediaPartner"/>. Id is a server-assigned Guid. Duplicate
/// detection is on the English name (case-insensitive) since a media
/// partner has no separate business code. Mirrors AdminCountryService /
/// AdminSpeakerService structure (inline validation + 409 on duplicate +
/// audit trail).</summary>
internal sealed class AdminMediaPartnerService(
    SimfAppDbContext appDbContext,
    IAssetService assetService,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<AdminMediaPartnerService> logger) : IAdminMediaPartnerService
{
    public async Task<GridPage<AdminMediaPartnerSummary>> ListAllAsync(GridQuery query, CancellationToken cancellationToken = default)
    {
        var (skip, top) = query.ClampPage(50, 500);

        var rows = appDbContext.MediaPartners.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(partner =>
                EF.Functions.Like(partner.Name, $"%{term}%")
                || EF.Functions.Like(partner.NameArabic, $"%{term}%"));
        }

        // CP grid per-column filters (D-255). Unknown columns are ignored.
        foreach (var (column, raw) in query.Filters)
        {
            if (string.IsNullOrWhiteSpace(raw)) { continue; }
            var v = raw.Trim();
            switch (column.ToLowerInvariant())
            {
                case "name":
                    rows = rows.Where(partner => partner.Name.Contains(v));
                    break;
                case "namearabic":
                    rows = rows.Where(partner => partner.NameArabic.Contains(v));
                    break;
                case "isactive":
                    if (bool.TryParse(v, out var isActive))
                    {
                        rows = rows.Where(partner => partner.IsActive == isActive);
                    }
                    break;
            }
        }

        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("name", true) => rows.OrderByDescending(partner => partner.Name),
            ("name", false) => rows.OrderBy(partner => partner.Name),
            ("namearabic", true) => rows.OrderByDescending(partner => partner.NameArabic),
            ("namearabic", false) => rows.OrderBy(partner => partner.NameArabic),
            ("displayorder", true) => rows.OrderByDescending(partner => partner.DisplayOrder),
            _ => rows.OrderBy(partner => partner.DisplayOrder)
                     .ThenBy(partner => partner.NameArabic),
        };

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows.Skip(skip).Take(top)
            .Select(partner => new AdminMediaPartnerSummary(
                partner.Id, partner.Name, partner.NameArabic,
                partner.LogoRelativePath, partner.Url, partner.DisplayOrder,
                partner.IsActive, partner.CreatedAt))
            .ToListAsync(cancellationToken);

        // The grid renders the real logo thumbnail only for rows with an active
        // MediaPartnerLogo asset (else an initials tile) — one batched query.
        var logoOwners = await assetService.WhichOwnersHaveActiveAssetAsync(
            AssetCategory.MediaPartnerLogo, page.Select(row => row.Id).ToList(), cancellationToken);
        page = page.Select(row => row with { HasLogo = logoOwners.Contains(row.Id) }).ToList();

        return GridPage<AdminMediaPartnerSummary>.Of(page, total,
            skip, top);
    }

    public async Task<AdminMediaPartnerDetail?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await appDbContext.MediaPartners.AsNoTracking()
            .Where(partner => partner.Id == id)
            .Select(partner => ToDetail(partner))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<AdminMediaPartnerDetail> CreateAsync(Guid actorUserId, AdminCreateMediaPartnerRequest request, CancellationToken cancellationToken = default)
    {
        var (name, nameArabic, logoRelativePath, url, displayOrder) = Validate(
            request.Name, request.NameArabic, request.LogoRelativePath,
            request.Url, request.DisplayOrder);

        var nameClash = await appDbContext.MediaPartners.AsNoTracking()
            .AnyAsync(p => p.Name == name, cancellationToken);
        if (nameClash)
        {
            throw new ApiException(ErrorCodes.MediaPartnerNameDuplicate, 409,
                $"A media partner named '{name}' already exists.",
                $"يوجد شريك إعلامي بالاسم '{name}' بالفعل.");
        }

        var now = timeProvider.GetUtcNow();
        var partner = new MediaPartner
        {
            Name = name,
            NameArabic = nameArabic,
            LogoRelativePath = logoRelativePath,
            Url = url,
            DisplayOrder = displayOrder,
            IsActive = true,
            CreatedAt = now,
        };

        appDbContext.MediaPartners.Add(partner);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.MediaPartnerCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={partner.Id}; name={name}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} created MediaPartner {Name} (id {Id})",
            actorUserId, name, partner.Id);

        return ToDetail(partner);
    }

    public async Task<AdminMediaPartnerDetail> UpdateAsync(Guid actorUserId, Guid id, AdminUpdateMediaPartnerRequest request, CancellationToken cancellationToken = default)
    {
        var partner = await appDbContext.MediaPartners
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new ApiException(ErrorCodes.NotFound, 404,
                "The media partner was not found.",
                "لم يتم العثور على الشريك الإعلامي.");

        var (name, nameArabic, logoRelativePath, url, displayOrder) = Validate(
            request.Name, request.NameArabic, request.LogoRelativePath,
            request.Url, request.DisplayOrder);

        if (!string.Equals(partner.Name, name, StringComparison.OrdinalIgnoreCase))
        {
            var clash = await appDbContext.MediaPartners.AsNoTracking()
                .AnyAsync(p => p.Id != id && p.Name == name, cancellationToken);
            if (clash)
            {
                throw new ApiException(ErrorCodes.MediaPartnerNameDuplicate, 409,
                    $"A media partner named '{name}' already exists.",
                    $"يوجد شريك إعلامي بالاسم '{name}' بالفعل.");
            }
        }

        partner.Name = name;
        partner.NameArabic = nameArabic;
        partner.LogoRelativePath = logoRelativePath;
        partner.Url = url;
        partner.DisplayOrder = displayOrder;
        partner.IsActive = request.IsActive;
        partner.UpdatedAt = timeProvider.GetUtcNow();

        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.MediaPartnerUpdated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={id}; name={name}; active={partner.IsActive}",
        }, cancellationToken);

        return ToDetail(partner);
    }

    public async Task DeactivateAsync(Guid actorUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var partner = await appDbContext.MediaPartners
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new ApiException(ErrorCodes.NotFound, 404,
                "The media partner was not found.",
                "لم يتم العثور على الشريك الإعلامي.");

        if (!partner.IsActive) { return; }

        partner.IsActive = false;
        partner.UpdatedAt = timeProvider.GetUtcNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.MediaPartnerDeactivated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={id}; name={partner.Name}",
        }, cancellationToken);
    }

    private static (string name, string nameArabic, string? logoRelativePath, string? url, int displayOrder) Validate(
        string nameRaw, string nameArabicRaw, string? logoRelativePathRaw, string? urlRaw, int displayOrderRaw)
    {
        var name = (nameRaw ?? string.Empty).Trim();
        if (name.Length is < 1 or > 256)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                "Media partner English name must be between 1 and 256 characters.",
                "يجب أن يتراوح الاسم الإنجليزي للشريك الإعلامي بين 1 و 256 حرفاً.");
        }
        var nameArabic = (nameArabicRaw ?? string.Empty).Trim();
        if (nameArabic.Length is < 1 or > 256)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                "Media partner Arabic name must be between 1 and 256 characters.",
                "يجب أن يتراوح الاسم العربي للشريك الإعلامي بين 1 و 256 حرفاً.");
        }
        var logoRelativePath = string.IsNullOrWhiteSpace(logoRelativePathRaw)
            ? null : logoRelativePathRaw.Trim();
        if (logoRelativePath is { Length: > 512 })
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                "Logo path must be 512 characters or fewer.",
                "يجب أن يكون مسار الشعار 512 حرفاً أو أقل.");
        }
        var url = string.IsNullOrWhiteSpace(urlRaw) ? null : urlRaw.Trim();
        if (url is { Length: > 512 })
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                "URL must be 512 characters or fewer.",
                "يجب أن يكون الرابط 512 حرفاً أو أقل.");
        }
        if (displayOrderRaw < 0)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                "Display order must be zero or a positive integer.",
                "يجب أن يكون ترتيب العرض صفراً أو عدداً صحيحاً موجباً.");
        }
        return (name, nameArabic, logoRelativePath, url, displayOrderRaw);
    }

    private static AdminMediaPartnerDetail ToDetail(MediaPartner partner) =>
        new(partner.Id, partner.Name, partner.NameArabic,
            partner.LogoRelativePath, partner.Url, partner.DisplayOrder,
            partner.IsActive, partner.CreatedAt, partner.UpdatedAt);
}
