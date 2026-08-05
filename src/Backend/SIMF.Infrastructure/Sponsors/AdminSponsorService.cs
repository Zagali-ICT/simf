// Tests: SIMF.Api.Tests/SponsorsTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Assets.Abstractions;
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
    IAssetService assetService,
    IAuditLog auditLog,
    TimeProvider timeProvider) : IAdminSponsorService
{
    public async Task<GridPage<AdminSponsorSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var (skip, top) = query.ClampPage(25, 200);

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
                sponsor.CreatedAt,
                sponsor.Tagline,
                sponsor.TaglineArabic,
                sponsor.About,
                sponsor.AboutArabic))
            .ToListAsync(cancellationToken);

        // The grid renders the real logo thumbnail only for rows with an active
        // SponsorLogo asset (else an initials tile) — one batched query, no N+1.
        var logoOwners = await assetService.WhichOwnersHaveActiveAssetAsync(
            AssetCategory.SponsorLogo, page.Select(row => row.Id).ToList(), cancellationToken);
        page = page.Select(row => row with { HasLogo = logoOwners.Contains(row.Id) }).ToList();

        return GridPage<AdminSponsorSummary>.Of(page, total,
            skip, top);
    }

    public async Task<AdminSponsorDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var sponsor = await appDbContext.Sponsors.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (sponsor is null) { return null; }
        var (en, ar) = await ResolveCountryAsync(sponsor.CountryId, cancellationToken);
        return ToDetail(sponsor, en, ar);
    }

    public async Task<AdminSponsorDetail> CreateAsync(
        Guid actorUserId, AdminCreateSponsorRequest request,
        CancellationToken cancellationToken = default)
    {
        var (nameEn, nameAr, tier, logoRelativePath, url, displayOrder) =
            Validate(request.NameEn, request.NameAr, request.Tier,
                request.LogoRelativePath, request.Url, request.DisplayOrder);
        ValidateContactFields(
            request.Email, request.PhonePrimary, request.PhoneSecondary,
            request.FacebookUrl, request.XUrl, request.LinkedInUrl, request.InstagramUrl,
            request.City, request.CityArabic,
            request.Latitude, request.Longitude);
        await EnsureCountryIsValidAsync(request.CountryId, cancellationToken);

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

        var now = timeProvider.SimfNow();
        var sponsor = new Sponsor
        {
            Id = Guid.NewGuid(),
            Name = nameEn,
            NameArabic = nameAr,
            Tier = tier,
            LogoRelativePath = logoRelativePath,
            Url = url,
            DisplayOrder = displayOrder,
            Tagline = NormaliseTagline(request.Tagline),
            TaglineArabic = NormaliseTagline(request.TaglineArabic),
            About = NormaliseAbout(request.About),
            AboutArabic = NormaliseAbout(request.AboutArabic),
            CountryId = request.CountryId,
            Email = NullIfBlank(request.Email),
            PhonePrimary = NullIfBlank(request.PhonePrimary),
            PhoneSecondary = NullIfBlank(request.PhoneSecondary),
            FacebookUrl = NullIfBlank(request.FacebookUrl),
            XUrl = NullIfBlank(request.XUrl),
            LinkedInUrl = NullIfBlank(request.LinkedInUrl),
            InstagramUrl = NullIfBlank(request.InstagramUrl),
            City = NullIfBlank(request.City),
            CityArabic = NullIfBlank(request.CityArabic),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            IsActive = true,
            CreatedAt = now,
        };

        appDbContext.Sponsors.Add(sponsor);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.SponsorCreated,
            actorUserId,
            $"sponsorId={sponsor.Id}; tier={tier}; nameAr={nameAr}",
            cancellationToken);

        var (en, ar) = await ResolveCountryAsync(sponsor.CountryId, cancellationToken);
        return ToDetail(sponsor, en, ar);
    }

    public async Task<AdminSponsorDetail> UpdateAsync(
        Guid actorUserId, Guid id, AdminUpdateSponsorRequest request,
        CancellationToken cancellationToken = default)
    {
        var (nameEn, nameAr, tier, logoRelativePath, url, displayOrder) =
            Validate(request.NameEn, request.NameAr, request.Tier,
                request.LogoRelativePath, request.Url, request.DisplayOrder);
        ValidateContactFields(
            request.Email, request.PhonePrimary, request.PhoneSecondary,
            request.FacebookUrl, request.XUrl, request.LinkedInUrl, request.InstagramUrl,
            request.City, request.CityArabic,
            request.Latitude, request.Longitude);
        await EnsureCountryIsValidAsync(request.CountryId, cancellationToken);

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
        sponsor.Tagline = NormaliseTagline(request.Tagline);
        sponsor.TaglineArabic = NormaliseTagline(request.TaglineArabic);
        sponsor.About = NormaliseAbout(request.About);
        sponsor.AboutArabic = NormaliseAbout(request.AboutArabic);
        sponsor.CountryId = request.CountryId;
        sponsor.Email = NullIfBlank(request.Email);
        sponsor.PhonePrimary = NullIfBlank(request.PhonePrimary);
        sponsor.PhoneSecondary = NullIfBlank(request.PhoneSecondary);
        sponsor.FacebookUrl = NullIfBlank(request.FacebookUrl);
        sponsor.XUrl = NullIfBlank(request.XUrl);
        sponsor.LinkedInUrl = NullIfBlank(request.LinkedInUrl);
        sponsor.InstagramUrl = NullIfBlank(request.InstagramUrl);
        sponsor.City = NullIfBlank(request.City);
        sponsor.CityArabic = NullIfBlank(request.CityArabic);
        sponsor.Latitude = request.Latitude;
        sponsor.Longitude = request.Longitude;
        sponsor.IsActive = request.IsActive;
        sponsor.UpdatedAt = timeProvider.SimfNow();

        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.SponsorUpdated,
            actorUserId,
            $"sponsorId={sponsor.Id}; tier={tier}; active={sponsor.IsActive}",
            cancellationToken);

        var (en, ar) = await ResolveCountryAsync(sponsor.CountryId, cancellationToken);
        return ToDetail(sponsor, en, ar);
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
        sponsor.UpdatedAt = timeProvider.SimfNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.SponsorDeactivated,
            actorUserId,
            $"sponsorId={sponsor.Id}",
            cancellationToken);
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

    // D-766 — validates the identity-card fields inlined from the removed
    // shared Contact directory. Lengths mirror the EF configuration; latitude
    // and longitude are an all-or-nothing pair with real-world ranges.
    private static void ValidateContactFields(
        string? email, string? phonePrimary, string? phoneSecondary,
        string? facebook, string? x, string? linkedIn, string? instagram,
        string? city, string? cityArabic,
        double? latitude, double? longitude)
    {
        if (!string.IsNullOrWhiteSpace(email) && email.Length > 320)
        {
            throw Invalid("Email must be 320 characters or less.",
                "يجب ألا يتجاوز البريد الإلكتروني 320 حرفاً.");
        }
        foreach (var phone in new[] { phonePrimary, phoneSecondary })
        {
            if (!string.IsNullOrWhiteSpace(phone) && phone.Length > 32)
            {
                throw Invalid("Phone numbers must be 32 characters or less.",
                    "يجب ألا يتجاوز رقم الهاتف 32 حرفاً.");
            }
        }
        foreach (var social in new[] { facebook, x, linkedIn, instagram })
        {
            if (!string.IsNullOrWhiteSpace(social) && social.Length > 256)
            {
                throw Invalid("Social URLs must be 256 characters or less.",
                    "يجب ألا يتجاوز رابط الشبكات الاجتماعية 256 حرفاً.");
            }
        }
        foreach (var cityValue in new[] { city, cityArabic })
        {
            if (!string.IsNullOrWhiteSpace(cityValue) && cityValue.Length > 128)
            {
                throw Invalid("City must be 128 characters or less.",
                    "يجب ألا تتجاوز المدينة 128 حرفاً.");
            }
        }
        if (latitude is null != (longitude is null))
        {
            throw Invalid("Latitude and longitude must be provided together.",
                "يجب إدخال خط العرض وخط الطول معاً.");
        }
        if (latitude is < -90 or > 90)
        {
            throw Invalid("Latitude must be between -90 and 90.",
                "يجب أن يكون خط العرض بين -90 و 90.");
        }
        if (longitude is < -180 or > 180)
        {
            throw Invalid("Longitude must be between -180 and 180.",
                "يجب أن يكون خط الطول بين -180 و 180.");
        }
    }

    private static ApiException Invalid(string english, string arabic) =>
        new(ErrorCodes.SponsorInvalid, 400, english, arabic);

    private async Task EnsureCountryIsValidAsync(
        int? countryId, CancellationToken cancellationToken)
    {
        if (countryId is null) { return; }
        var exists = await appDbContext.Countries
            .AsNoTracking()
            .AnyAsync(country => country.Id == countryId.Value && country.IsActive, cancellationToken);
        if (!exists)
        {
            throw new ApiException(ErrorCodes.SponsorInvalid, 400,
                $"Country id '{countryId}' does not exist or is inactive.",
                $"رقم البلد '{countryId}' غير موجود أو غير مفعّل.");
        }
    }

    private async Task<(string? en, string? ar)> ResolveCountryAsync(
        int? countryId, CancellationToken cancellationToken)
    {
        if (countryId is null) { return (null, null); }
        var row = await appDbContext.Countries
            .AsNoTracking()
            .Where(country => country.Id == countryId.Value)
            .Select(country => new { country.Name, country.NameArabic })
            .SingleOrDefaultAsync(cancellationToken);
        return (row?.Name, row?.NameArabic);
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static AdminSponsorDetail ToDetail(
        Sponsor sponsor, string? countryNameEn, string? countryNameAr) =>
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
            sponsor.Tagline,
            sponsor.TaglineArabic,
            sponsor.About,
            sponsor.AboutArabic,
            sponsor.CountryId,
            countryNameEn,
            countryNameAr,
            sponsor.Email,
            sponsor.PhonePrimary,
            sponsor.PhoneSecondary,
            sponsor.FacebookUrl,
            sponsor.XUrl,
            sponsor.LinkedInUrl,
            sponsor.InstagramUrl,
            sponsor.City,
            sponsor.CityArabic,
            sponsor.Latitude,
            sponsor.Longitude);

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
