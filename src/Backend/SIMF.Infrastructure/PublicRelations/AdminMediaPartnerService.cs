// Tests: SIMF.Api.Tests/AdminMediaPartnersTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
/// audit trail).
///
/// NOTE (central integration): this service intentionally uses the generic
/// <see cref="ErrorCodes.NotFound"/> / <see cref="ErrorCodes.MediaPartnerNameDuplicate"/> /
/// <see cref="ErrorCodes.ValidationFailed"/> codes and literal audit-event
/// strings so the module compiles without touching the shared
/// <c>ErrorCodes</c> / <c>AuditEvents</c> files (parallel-agent safety).
/// Promote these to dedicated MediaPartner* constants during integration —
/// see the module's integration notes.</summary>
internal sealed class AdminMediaPartnerService(
    SimfAppDbContext appDbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<AdminMediaPartnerService> logger) : IAdminMediaPartnerService
{
    private const string EventCreated = "MediaPartnerCreated";
    private const string EventUpdated = "MediaPartnerUpdated";
    private const string EventDeactivated = "MediaPartnerDeactivated";

    public async Task<GridPage<AdminMediaPartnerSummary>> ListAllAsync(GridQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 50, 1, 500);

        var rows = appDbContext.MediaPartners.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(partner =>
                EF.Functions.Like(partner.NameEn, $"%{term}%")
                || EF.Functions.Like(partner.NameAr, $"%{term}%"));
        }

        if (query.Filters.TryGetValue("isActive", out var activeFilter) && bool.TryParse(activeFilter, out var isActive))
        {
            rows = rows.Where(partner => partner.IsActive == isActive);
        }

        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("nameen", true) => rows.OrderByDescending(partner => partner.NameEn),
            ("nameen", false) => rows.OrderBy(partner => partner.NameEn),
            ("namear", true) => rows.OrderByDescending(partner => partner.NameAr),
            ("namear", false) => rows.OrderBy(partner => partner.NameAr),
            ("displayorder", true) => rows.OrderByDescending(partner => partner.DisplayOrder),
            _ => rows.OrderBy(partner => partner.DisplayOrder)
                     .ThenBy(partner => partner.NameAr),
        };

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows.Skip(skip).Take(top)
            .Select(partner => new AdminMediaPartnerSummary(
                partner.Id, partner.NameEn, partner.NameAr,
                partner.LogoRelativePath, partner.Url, partner.DisplayOrder,
                partner.IsActive, partner.CreatedAt))
            .ToListAsync(cancellationToken);

        return GridPage<AdminMediaPartnerSummary>.Of(page, total,
            new GridQuery { Skip = skip, Top = top });
    }

    public async Task<AdminMediaPartnerDetail?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await appDbContext.MediaPartners.AsNoTracking()
            .Where(partner => partner.Id == id)
            .Select(partner => ToDetail(partner))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<AdminMediaPartnerDetail> CreateAsync(Guid actorUserId, AdminCreateMediaPartnerRequest request, CancellationToken cancellationToken = default)
    {
        var (nameEn, nameAr, logoRelativePath, url, displayOrder) = Validate(
            request.NameEn, request.NameAr, request.LogoRelativePath,
            request.Url, request.DisplayOrder);

        var nameClash = await appDbContext.MediaPartners.AsNoTracking()
            .AnyAsync(p => p.NameEn == nameEn, cancellationToken);
        if (nameClash)
        {
            throw new ApiException(ErrorCodes.MediaPartnerNameDuplicate, 409,
                $"A media partner named '{nameEn}' already exists.",
                $"يوجد شريك إعلامي بالاسم '{nameEn}' بالفعل.");
        }

        var now = timeProvider.GetUtcNow();
        var partner = new MediaPartner
        {
            NameEn = nameEn,
            NameAr = nameAr,
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
            EventType = EventCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={partner.Id}; nameEn={nameEn}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} created MediaPartner {NameEn} (id {Id})",
            actorUserId, nameEn, partner.Id);

        return ToDetail(partner);
    }

    public async Task<AdminMediaPartnerDetail> UpdateAsync(Guid actorUserId, Guid id, AdminUpdateMediaPartnerRequest request, CancellationToken cancellationToken = default)
    {
        var partner = await appDbContext.MediaPartners
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new ApiException(ErrorCodes.NotFound, 404,
                "The media partner was not found.",
                "لم يتم العثور على الشريك الإعلامي.");

        var (nameEn, nameAr, logoRelativePath, url, displayOrder) = Validate(
            request.NameEn, request.NameAr, request.LogoRelativePath,
            request.Url, request.DisplayOrder);

        if (!string.Equals(partner.NameEn, nameEn, StringComparison.OrdinalIgnoreCase))
        {
            var clash = await appDbContext.MediaPartners.AsNoTracking()
                .AnyAsync(p => p.Id != id && p.NameEn == nameEn, cancellationToken);
            if (clash)
            {
                throw new ApiException(ErrorCodes.MediaPartnerNameDuplicate, 409,
                    $"A media partner named '{nameEn}' already exists.",
                    $"يوجد شريك إعلامي بالاسم '{nameEn}' بالفعل.");
            }
        }

        partner.NameEn = nameEn;
        partner.NameAr = nameAr;
        partner.LogoRelativePath = logoRelativePath;
        partner.Url = url;
        partner.DisplayOrder = displayOrder;
        partner.IsActive = request.IsActive;
        partner.UpdatedAt = timeProvider.GetUtcNow();

        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = EventUpdated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={id}; nameEn={nameEn}; active={partner.IsActive}",
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
            EventType = EventDeactivated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={id}; nameEn={partner.NameEn}",
        }, cancellationToken);
    }

    private static (string nameEn, string nameAr, string? logoRelativePath, string? url, int displayOrder) Validate(
        string nameEnRaw, string nameArRaw, string? logoRelativePathRaw, string? urlRaw, int displayOrderRaw)
    {
        var nameEn = (nameEnRaw ?? string.Empty).Trim();
        if (nameEn.Length is < 1 or > 256)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                "Media partner English name must be between 1 and 256 characters.",
                "يجب أن يتراوح الاسم الإنجليزي للشريك الإعلامي بين 1 و 256 حرفاً.");
        }
        var nameAr = (nameArRaw ?? string.Empty).Trim();
        if (nameAr.Length is < 1 or > 256)
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
        return (nameEn, nameAr, logoRelativePath, url, displayOrderRaw);
    }

    private static AdminMediaPartnerDetail ToDetail(MediaPartner partner) =>
        new(partner.Id, partner.NameEn, partner.NameAr,
            partner.LogoRelativePath, partner.Url, partner.DisplayOrder,
            partner.IsActive, partner.CreatedAt, partner.UpdatedAt);
}
