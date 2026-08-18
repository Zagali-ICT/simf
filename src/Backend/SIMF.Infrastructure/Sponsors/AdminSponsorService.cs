// Tests: SIMF.Api.Tests/SponsorsTests.cs
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Assets.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.Sponsors.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Grids;
using SIMF.Contracts.Admin;
using SIMF.Domain.Sponsors;
using SIMF.Infrastructure.Common.Grids;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Sponsors;

/// <summary>Admin CRUD over <see cref="Sponsor"/>.
/// Mirrors AdminDelegationService structure (validation → mutate → save →
/// audit). Soft-delete via <see cref="SIMF.Domain.Common.BaseAuditEntity.Deactivate"/>.</summary>
internal sealed class AdminSponsorService(
    SimfAppDbContext appDbContext,
    IAssetService assetService,
    IAuditLog auditLog,
    TimeProvider timeProvider) : IAdminSponsorService
{
    /// <summary>
    /// The grid contract for /admin/sponsors: one entry per key SponsorsList.razor
    /// can send, as both its filter and its sort. A key not declared here is a 400,
    /// not a silently ignored request.
    /// </summary>
    private static readonly GridColumns<Sponsor> Columns = new GridColumns<Sponsor>()
        .Add("nameEn", sponsor => sponsor.Name, searchable: true)
        .Add("nameAr", sponsor => sponsor.NameArabic, searchable: true)
        .Add("tier", sponsor => sponsor.Tier)
        .Add("displayOrder", sponsor => sponsor.DisplayOrder)
        .Add("isActive", sponsor => sponsor.IsActive)
        // Tier, then DisplayOrder, then NameAr — the public ordering.
        .DefaultOrder("tier")
        .DefaultOrder("displayOrder")
        .DefaultOrder("nameAr")
        .PageSize(fallback: 25, max: 200);

    private static readonly Expression<Func<Sponsor, AdminSponsorSummary>> ToSummary =
        sponsor => new AdminSponsorSummary(
            sponsor.Id,
            sponsor.Name,
            sponsor.NameArabic,
            (int)sponsor.Tier,
            sponsor.Tier.ToString(),
            null,
            sponsor.Url,
            sponsor.DisplayOrder,
            sponsor.IsActive,
            sponsor.CreatedAt,
            sponsor.Tagline,
            sponsor.TaglineArabic,
            sponsor.About,
            sponsor.AboutArabic);

    public async Task<GridPage<AdminSponsorSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var page = await appDbContext.Sponsors.ToGridPageAsync(
            query, Columns, sponsor => sponsor.Id, ToSummary, cancellationToken);

        // The grid renders the real logo thumbnail only for rows with an active
        // SponsorLogo asset (else an initials tile) — one batched query, no N+1.
        var logoOwners = await assetService.WhichOwnersHaveActiveAssetAsync(
            AssetCategory.SponsorLogo,
            page.Items.Select(row => row.Id).ToList(),
            cancellationToken);

        return GridPage<AdminSponsorSummary>.Of(
            page.Items.Select(row => row with { HasLogo = logoOwners.Contains(row.Id) }).ToList(),
            page.Total, page.Skip, page.Top);
    }

    public async Task<AdminSponsorDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var sponsor = await appDbContext.Sponsors.AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        if (sponsor is null) { return null; }
        var (countryNameEn, countryNameAr) =
            await ResolveCountryAsync(sponsor.CountryId, cancellationToken);
        return ToDetail(sponsor, countryNameEn, countryNameAr);
    }

    public async Task<AdminSponsorDetail> CreateAsync(
        Guid actorUserId, AdminCreateSponsorRequest request,
        CancellationToken cancellationToken = default)
    {
        var (nameEn, nameAr, tier, url, displayOrder) =
            Validate(request.NameEn, request.NameAr, request.Tier,
                request.Url, request.DisplayOrder);
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

        var (countryNameEn, countryNameAr) =
            await ResolveCountryAsync(sponsor.CountryId, cancellationToken);
        return ToDetail(sponsor, countryNameEn, countryNameAr);
    }

    public async Task<AdminSponsorDetail> UpdateAsync(
        Guid actorUserId, Guid id, AdminUpdateSponsorRequest request,
        CancellationToken cancellationToken = default)
    {
        var (nameEn, nameAr, tier, url, displayOrder) =
            Validate(request.NameEn, request.NameAr, request.Tier,
                request.Url, request.DisplayOrder);
        ValidateContactFields(
            request.Email, request.PhonePrimary, request.PhoneSecondary,
            request.FacebookUrl, request.XUrl, request.LinkedInUrl, request.InstagramUrl,
            request.City, request.CityArabic,
            request.Latitude, request.Longitude);
        await EnsureCountryIsValidAsync(request.CountryId, cancellationToken);

        var sponsor = await appDbContext.Sponsors
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw new ApiException(ErrorCodes.SponsorNotFound, 404,
                "The sponsor was not found.",
                "لم يتم العثور على الراعي.");

        // Reactivation is a clash path of its own: the duplicate rule only counts
        // ACTIVE rows, so bringing a retired sponsor back can collide with a name
        // that went live in the meantime WITHOUT the name or tier moving at all.
        // Checking only the moved-key case let an update create exactly the pair
        // CreateAsync refuses.
        var renamedOrRetiered =
            !string.Equals(sponsor.NameArabic, nameAr, StringComparison.Ordinal)
            || sponsor.Tier != tier;
        if (request.IsActive && (renamedOrRetiered || !sponsor.IsActive))
        {
            var clash = await appDbContext.Sponsors.AsNoTracking().AnyAsync(
                other => other.Id != id
                    && other.IsActive
                    && other.Tier == tier
                    && other.NameArabic == nameAr,
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

        var (countryNameEn, countryNameAr) =
            await ResolveCountryAsync(sponsor.CountryId, cancellationToken);
        return ToDetail(sponsor, countryNameEn, countryNameAr);
    }

    public async Task DeactivateAsync(
        Guid actorUserId, Guid id,
        CancellationToken cancellationToken = default)
    {
        var sponsor = await appDbContext.Sponsors
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
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

    private static (string NameEn, string NameAr, SponsorTier Tier,
        string? Url, int DisplayOrder) Validate(
            string nameEnRaw, string nameArRaw, int tierRaw,
            string? urlRaw, int displayOrderRaw)
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

        return (nameEn, nameAr, tier, url, displayOrderRaw);
    }

    // Validates the identity-card fields inlined from the removed
    // shared Contact directory. Lengths mirror the EF configuration; latitude
    // and longitude are an all-or-nothing pair with real-world ranges.
    private static void ValidateContactFields(
        string? email, string? phonePrimary, string? phoneSecondary,
        string? facebook, string? xUrl, string? linkedIn, string? instagram,
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
        foreach (var url in new[] { facebook, xUrl, linkedIn, instagram })
        {
            if (!string.IsNullOrWhiteSpace(url) && url.Length > 256)
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

    private async Task<(string? NameEn, string? NameAr)> ResolveCountryAsync(
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
            null,
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

    // Trim a tagline to null when blank; enforce the 256-char limit
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
