// Tests: SIMF.Api.Tests/AdminMediaPartnersTests.cs
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Assets.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.PublicRelations.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Grids;
using SIMF.Contracts.Admin;
using SIMF.Contracts.PublicRelations;
using SIMF.Domain.PublicRelations;
using SIMF.Infrastructure.Common.Grids;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.PublicRelations;

/// <summary>Admin CRUD over
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
    /// <summary>
    /// The grid contract for /admin/media-partners: one entry per key
    /// MediaPartnersList.razor can send, as both its filter and its sort. A key not
    /// declared here is a 400, not a silently ignored request.
    /// </summary>
    private static readonly GridColumns<MediaPartner> Columns = new GridColumns<MediaPartner>()
        .Add("name", partner => partner.Name, searchable: true)
        .Add("nameArabic", partner => partner.NameArabic, searchable: true)
        .Add("displayOrder", partner => partner.DisplayOrder)
        .Add("isActive", partner => partner.IsActive)
        .DefaultOrder("displayOrder")
        .DefaultOrder("nameArabic")
        .PageSize(fallback: 50, max: 500);

    // HasLogo is projected false and filled in below: "an active MediaPartnerLogo
    // asset exists" is a file-store fact, not a column on MediaPartner, so it
    // cannot be part of the SELECT.
    private static readonly Expression<Func<MediaPartner, AdminMediaPartnerSummary>> ToSummary =
        partner => new AdminMediaPartnerSummary(
            partner.Id, partner.Name, partner.NameArabic,
            null, partner.Url, partner.DisplayOrder,
            partner.IsActive, partner.CreatedAt, false);

    public async Task<GridPage<AdminMediaPartnerSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var page = await appDbContext.MediaPartners.ToGridPageAsync(
            query, Columns, partner => partner.Id, ToSummary, cancellationToken);

        // The grid renders the real logo thumbnail only for rows with an active
        // MediaPartnerLogo asset (else an initials tile) — one batched query.
        var logoOwners = await assetService.WhichOwnersHaveActiveAssetAsync(
            AssetCategory.MediaPartnerLogo,
            page.Items.Select(row => row.Id).ToList(),
            cancellationToken);

        return GridPage<AdminMediaPartnerSummary>.Of(
            page.Items
                .Select(row => row with { HasLogo = logoOwners.Contains(row.Id) })
                .ToList(),
            page.Total,
            page.Skip,
            page.Top);
    }

    public async Task<AdminMediaPartnerDetail?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var partner = await appDbContext.MediaPartners.AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        if (partner is null) { return null; }
        var (countryNameEn, countryNameAr) =
            await ResolveCountryAsync(partner.CountryId, cancellationToken);
        return ToDetail(partner, countryNameEn, countryNameAr);
    }

    public async Task<AdminMediaPartnerDetail> CreateAsync(Guid actorUserId, AdminCreateMediaPartnerRequest request, CancellationToken cancellationToken = default)
    {
        var (name, nameArabic, url, displayOrder) = Validate(
            request.Name, request.NameArabic,
            request.Url, request.DisplayOrder);
        ValidateContactFields(
            request.Email, request.PhonePrimary, request.PhoneSecondary,
            request.FacebookUrl, request.XUrl, request.LinkedInUrl, request.InstagramUrl,
            request.City, request.CityArabic,
            request.Latitude, request.Longitude);
        await EnsureCountryIsValidAsync(request.CountryId, cancellationToken);

        var nameClash = await appDbContext.MediaPartners.AsNoTracking()
            .AnyAsync(row => row.Name == name, cancellationToken);
        if (nameClash)
        {
            throw new ApiException(ErrorCodes.MediaPartnerNameDuplicate, 409,
                $"A media partner named '{name}' already exists.",
                $"يوجد شريك إعلامي بالاسم '{name}' بالفعل.");
        }

        var now = timeProvider.SimfNow();
        var partner = new MediaPartner
        {
            Name = name,
            NameArabic = nameArabic,
            Url = url,
            DisplayOrder = displayOrder,
            Email = NullIfBlank(request.Email),
            PhonePrimary = NullIfBlank(request.PhonePrimary),
            PhoneSecondary = NullIfBlank(request.PhoneSecondary),
            FacebookUrl = NullIfBlank(request.FacebookUrl),
            XUrl = NullIfBlank(request.XUrl),
            LinkedInUrl = NullIfBlank(request.LinkedInUrl),
            InstagramUrl = NullIfBlank(request.InstagramUrl),
            City = NullIfBlank(request.City),
            CityArabic = NullIfBlank(request.CityArabic),
            CountryId = request.CountryId,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            IsActive = true,
            CreatedAt = now,
        };

        appDbContext.MediaPartners.Add(partner);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.MediaPartnerCreated,
            actorUserId,
            $"id={partner.Id}; name={name}",
            cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} created MediaPartner {Name} (id {Id})",
            actorUserId, name, partner.Id);

        var (countryNameEn, countryNameAr) =
            await ResolveCountryAsync(partner.CountryId, cancellationToken);
        return ToDetail(partner, countryNameEn, countryNameAr);
    }

    public async Task<AdminMediaPartnerDetail> UpdateAsync(Guid actorUserId, Guid id, AdminUpdateMediaPartnerRequest request, CancellationToken cancellationToken = default)
    {
        var partner = await appDbContext.MediaPartners
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw new ApiException(ErrorCodes.NotFound, 404,
                "The media partner was not found.",
                "لم يتم العثور على الشريك الإعلامي.");

        var (name, nameArabic, url, displayOrder) = Validate(
            request.Name, request.NameArabic,
            request.Url, request.DisplayOrder);
        ValidateContactFields(
            request.Email, request.PhonePrimary, request.PhoneSecondary,
            request.FacebookUrl, request.XUrl, request.LinkedInUrl, request.InstagramUrl,
            request.City, request.CityArabic,
            request.Latitude, request.Longitude);
        await EnsureCountryIsValidAsync(request.CountryId, cancellationToken);

        if (!string.Equals(partner.Name, name, StringComparison.OrdinalIgnoreCase))
        {
            var clash = await appDbContext.MediaPartners.AsNoTracking()
                .AnyAsync(other => other.Id != id && other.Name == name, cancellationToken);
            if (clash)
            {
                throw new ApiException(ErrorCodes.MediaPartnerNameDuplicate, 409,
                    $"A media partner named '{name}' already exists.",
                    $"يوجد شريك إعلامي بالاسم '{name}' بالفعل.");
            }
        }

        partner.Name = name;
        partner.NameArabic = nameArabic;
        partner.Url = url;
        partner.DisplayOrder = displayOrder;
        partner.Email = NullIfBlank(request.Email);
        partner.PhonePrimary = NullIfBlank(request.PhonePrimary);
        partner.PhoneSecondary = NullIfBlank(request.PhoneSecondary);
        partner.FacebookUrl = NullIfBlank(request.FacebookUrl);
        partner.XUrl = NullIfBlank(request.XUrl);
        partner.LinkedInUrl = NullIfBlank(request.LinkedInUrl);
        partner.InstagramUrl = NullIfBlank(request.InstagramUrl);
        partner.City = NullIfBlank(request.City);
        partner.CityArabic = NullIfBlank(request.CityArabic);
        partner.CountryId = request.CountryId;
        partner.Latitude = request.Latitude;
        partner.Longitude = request.Longitude;
        partner.IsActive = request.IsActive;
        partner.UpdatedAt = timeProvider.SimfNow();

        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.MediaPartnerUpdated,
            actorUserId,
            $"id={id}; name={name}; active={partner.IsActive}",
            cancellationToken);

        var (countryNameEn, countryNameAr) =
            await ResolveCountryAsync(partner.CountryId, cancellationToken);
        return ToDetail(partner, countryNameEn, countryNameAr);
    }

    public async Task DeactivateAsync(Guid actorUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var partner = await appDbContext.MediaPartners
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw new ApiException(ErrorCodes.NotFound, 404,
                "The media partner was not found.",
                "لم يتم العثور على الشريك الإعلامي.");

        if (!partner.IsActive) { return; }

        partner.IsActive = false;
        partner.UpdatedAt = timeProvider.SimfNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.MediaPartnerDeactivated,
            actorUserId,
            $"id={id}; name={partner.Name}",
            cancellationToken);
    }

    private static (string Name, string NameArabic, string? Url, int DisplayOrder) Validate(
        string nameRaw, string nameArabicRaw, string? urlRaw, int displayOrderRaw)
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
        return (name, nameArabic, url, displayOrderRaw);
    }

    // Validates the identity-card fields inlined from the removed shared
    // Contact directory. Lengths mirror the EF configuration; latitude and
    // longitude are an all-or-nothing pair with real-world ranges.
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
        new(ErrorCodes.ValidationFailed, 400, english, arabic);

    private async Task EnsureCountryIsValidAsync(
        int? countryId, CancellationToken cancellationToken)
    {
        if (countryId is null) { return; }
        var exists = await appDbContext.Countries
            .AsNoTracking()
            .AnyAsync(country => country.Id == countryId.Value && country.IsActive, cancellationToken);
        if (!exists)
        {
            throw Invalid(
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

    private static AdminMediaPartnerDetail ToDetail(
        MediaPartner partner, string? countryNameEn, string? countryNameAr) =>
        new(partner.Id, partner.Name, partner.NameArabic,
            null, partner.Url, partner.DisplayOrder,
            partner.IsActive, partner.CreatedAt, partner.UpdatedAt,
            partner.Email, partner.PhonePrimary, partner.PhoneSecondary,
            partner.FacebookUrl, partner.XUrl, partner.LinkedInUrl,
            partner.InstagramUrl, partner.City, partner.CityArabic,
            partner.CountryId, countryNameEn, countryNameAr,
            partner.Latitude, partner.Longitude);
}
