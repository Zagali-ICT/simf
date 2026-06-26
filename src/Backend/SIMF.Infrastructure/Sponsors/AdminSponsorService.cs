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
                EF.Functions.Like(sponsor.Name, $"%{term}%")
                || EF.Functions.Like(sponsor.NameArabic, $"%{term}%"));
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
                    rows = rows.Where(sponsor => sponsor.Name.Contains(v));
                    break;
                case "namear":
                    rows = rows.Where(sponsor => sponsor.NameArabic.Contains(v));
                    break;
            }
        }

        // CP grid sortable columns (D-255). Default (and any unknown sort):
        // Tier, then DisplayOrder, then NameAr — the public ordering.
        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("nameen", false) => rows.OrderBy(sponsor => sponsor.Name),
            ("nameen", true) => rows.OrderByDescending(sponsor => sponsor.Name),
            ("namear", false) => rows.OrderBy(sponsor => sponsor.NameArabic),
            ("namear", true) => rows.OrderByDescending(sponsor => sponsor.NameArabic),
            ("tier", false) => rows.OrderBy(sponsor => sponsor.Tier),
            ("tier", true) => rows.OrderByDescending(sponsor => sponsor.Tier),
            ("displayorder", false) => rows.OrderBy(sponsor => sponsor.DisplayOrder),
            ("displayorder", true) => rows.OrderByDescending(sponsor => sponsor.DisplayOrder),
            ("isactive", false) => rows.OrderBy(sponsor => sponsor.IsActive),
            ("isactive", true) => rows.OrderByDescending(sponsor => sponsor.IsActive),
            _ => rows
                .OrderBy(sponsor => sponsor.Tier)
                .ThenBy(sponsor => sponsor.DisplayOrder)
                .ThenBy(sponsor => sponsor.NameArabic),
        };

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows
            .Skip(skip).Take(top)
            .Select(sponsor => new AdminSponsorSummary(
                sponsor.Id,
                sponsor.Name,
                sponsor.NameArabic,
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
        await EnsureContactIsValidAsync(request.ContactId, cancellationToken);

        // Duplicate guard: an active sponsor with the same Arabic name in the
        // same tier is treated as a clash (matches the Country code-clash 409
        // pattern). Inactive rows do not block re-create.
        var clash = await appDbContext.Sponsors.AsNoTracking().AnyAsync(
            sponsor => sponsor.IsActive
                && sponsor.Tier == tier
                && sponsor.NameArabic == nameAr,
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
            Name = nameEn,
            NameArabic = nameAr,
            Tier = tier,
            LogoRelativePath = logoRelativePath,
            Url = url,
            DisplayOrder = displayOrder,
            ContactId = request.ContactId,
            Tagline = NormaliseTagline(request.Tagline),
            TaglineArabic = NormaliseTagline(request.TaglineArabic),
            About = NormaliseAbout(request.About),
            AboutArabic = NormaliseAbout(request.AboutArabic),
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
        await EnsureContactIsValidAsync(request.ContactId, cancellationToken);

        var sponsor = await appDbContext.Sponsors
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new ApiException(ErrorCodes.SponsorNotFound, 404,
                "The sponsor was not found.",
                "لم يتم العثور على الراعي.");

        var renamedOrRetiered =
            !string.Equals(sponsor.NameArabic, nameAr, StringComparison.Ordinal)
            || sponsor.Tier != tier;
        if (request.IsActive && renamedOrRetiered)
        {
            var clash = await appDbContext.Sponsors.AsNoTracking().AnyAsync(
                s => s.Id != id
                    && s.IsActive
                    && s.Tier == tier
                    && s.NameArabic == nameAr,
                cancellationToken);
            if (clash)
            {
                throw new ApiException(ErrorCodes.SponsorDuplicate, 409,
                    $"An active sponsor named '{nameAr}' already exists in this tier.",
                    $"يوجد راعٍ نشط بالاسم '{nameAr}' في هذه الفئة بالفعل.");
            }
        }

        sponsor.Name = nameEn;
        sponsor.NameArabic = nameAr;
        sponsor.Tier = tier;
        sponsor.LogoRelativePath = logoRelativePath;
        sponsor.Url = url;
        sponsor.DisplayOrder = displayOrder;
        sponsor.ContactId = request.ContactId;
        sponsor.Tagline = NormaliseTagline(request.Tagline);
        sponsor.TaglineArabic = NormaliseTagline(request.TaglineArabic);
        sponsor.About = NormaliseAbout(request.About);
        sponsor.AboutArabic = NormaliseAbout(request.AboutArabic);
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

    // SIMF-FDS-014 (D-281) — the optional shared-Contact link must point at an
    // existing active Contact. Clean 400 instead of a DB FK-violation 500.
    private async Task EnsureContactIsValidAsync(
        Guid? contactId, CancellationToken cancellationToken)
    {
        if (contactId is null) { return; }
        var exists = await appDbContext.Contacts
            .AsNoTracking()
            .AnyAsync(contact => contact.Id == contactId.Value && contact.IsActive, cancellationToken);
        if (!exists)
        {
            throw new ApiException(ErrorCodes.SponsorInvalid, 400,
                $"Contact id '{contactId}' does not exist or is inactive.",
                $"جهة الاتصال '{contactId}' غير موجودة أو غير مفعّلة.");
        }
    }

    private static AdminSponsorDetail ToDetail(Sponsor sponsor) =>
        new(sponsor.Id,
            sponsor.Name,
            sponsor.NameArabic,
            (int)sponsor.Tier,
            sponsor.Tier.ToString(),
            sponsor.LogoRelativePath,
            sponsor.Url,
            sponsor.DisplayOrder,
            sponsor.IsActive,
            sponsor.CreatedAt,
            sponsor.UpdatedAt,
            sponsor.ContactId,
            sponsor.Tagline,
            sponsor.TaglineArabic,
            sponsor.About,
            sponsor.AboutArabic);

    // D-432 — trim a tagline to null when blank; enforce the 256-char limit
    // (mirrors SponsorConfiguration.HasMaxLength + the CP MaxLength) so a direct
    // API call gets a clean 400 instead of a DB error.
    private static string? NormaliseTagline(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }
        if (trimmed.Length > 256)
        {
            throw new ApiException(ErrorCodes.SponsorInvalid, 400,
                "Tagline must be at most 256 characters.",
                "يجب ألا يتجاوز النص التعريفي 256 حرفًا.");
        }
        return trimmed;
    }

    // Trim an about paragraph to null when blank; enforce the 2048-char limit
    // (mirrors SponsorConfiguration.HasMaxLength + the CP MaxLength) so a direct
    // API call gets a clean 400 instead of a DB error.
    private static string? NormaliseAbout(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }
        if (trimmed.Length > 2048)
        {
            throw new ApiException(ErrorCodes.SponsorInvalid, 400,
                "About must be at most 2048 characters.",
                "يجب ألا يتجاوز النص التعريفي 2048 حرفًا.");
        }
        return trimmed;
    }
}
