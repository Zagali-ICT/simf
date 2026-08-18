// Tests: SIMF.Api.Tests/AdminCountriesTests.cs, SIMF.Api.Tests/DelegationsTests.cs
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Common.Abstractions;
using SIMF.Common;
using SIMF.Common.Grids;
using SIMF.Contracts.Admin;
using SIMF.Domain.Common;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Common.Grids;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Common;

/// <summary>Admin CRUD over <see cref="Country"/>. Id is the ISO
/// 3166-1 numeric code, manually assigned at create time (caller is
/// responsible for picking a free id; service validates uniqueness).
/// Mirrors AdminThemeService / AdminHallService structure.</summary>
internal sealed class AdminCountryService(
    SimfAppDbContext appDbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<AdminCountryService> logger) : IAdminCountryService
{
    /// <summary>
    /// The grid contract for /admin/countries: one entry per key a caller can
    /// send. <c>isActive</c> is not a filter box on CountriesList — it is the
    /// active-rows filter the Booths / Exhibitors / MediaPartner / Speakers /
    /// Sponsors pickers all send when they load their country list.
    /// </summary>
    private static readonly GridColumns<Country> Columns = new GridColumns<Country>()
        .Add("id", country => country.Id)
        .Add("code", country => country.Code, searchable: true)
        .Add("name", country => country.Name, searchable: true)
        .Add("nameArabic", country => country.NameArabic, searchable: true)
        .Add("displayOrder", country => country.DisplayOrder)
        .Add("isActive", country => country.IsActive)
        .DefaultOrder("displayOrder")
        .DefaultOrder("name")
        .PageSize(fallback: 50, max: 500);

    private static readonly Expression<Func<Country, AdminCountrySummary>> ToSummary =
        country => new AdminCountrySummary(
            country.Id, country.Code, country.Name, country.NameArabic,
            country.PhonePrefix, country.DisplayOrder,
            country.IsActive, country.CreatedAt, country.IsInvited,
            country.DelegationArrivalDate, country.DelegationDepartureDate);

    public Task<GridPage<AdminCountrySummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default) =>
        appDbContext.Countries.ToGridPageAsync(
            query, Columns, country => country.Id, ToSummary, cancellationToken);

    public async Task<AdminCountryDetail?> GetAsync(int id, CancellationToken cancellationToken = default) =>
        await appDbContext.Countries.AsNoTracking()
            .Where(country => country.Id == id)
            .Select(country => ToDetail(country))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<AdminCountryDetail> CreateAsync(Guid actorUserId, AdminCreateCountryRequest request, CancellationToken cancellationToken = default)
    {
        var (id, code, name, nameArabic, phonePrefix, displayOrder) = Validate(
            request.Id, request.Code, request.Name, request.NameArabic,
            request.PhonePrefix, request.DisplayOrder);

        var idClash = await appDbContext.Countries.AsNoTracking().AnyAsync(c => c.Id == id, cancellationToken);
        if (idClash)
        {
            throw new ApiException(ErrorCodes.CountryIdDuplicate, 409,
                $"A country with id {id} already exists.",
                $"يوجد بلد بالمعرّف {id} بالفعل.");
        }

        var codeClash = await appDbContext.Countries.AsNoTracking().AnyAsync(c => c.Code == code, cancellationToken);
        if (codeClash)
        {
            throw new ApiException(ErrorCodes.CountryCodeDuplicate, 409,
                $"A country with code '{code}' already exists.",
                $"يوجد بلد بالرمز '{code}' بالفعل.");
        }

        // Delegation data (head + dates) only applies to an invited
        // country; drop it otherwise so a non-invited row never stores orphaned
        // head/date data that would silently resurface if it were re-invited. The
        // head, when supplied, must be an active delegate of this country (none
        // exist yet on create, so the CP create form never sends one — but guard
        // anyway for API callers).
        var (headId, arrival, departure) = NormalizeDelegation(request.IsInvited,
            request.HeadOfDelegationUserProfileId,
            request.DelegationArrivalDate, request.DelegationDepartureDate);
        await ValidateHeadAsync(id, headId, cancellationToken);

        var now = timeProvider.SimfNow();
        var country = new Country
        {
            Id = id,
            Code = code,
            Name = name,
            NameArabic = nameArabic,
            PhonePrefix = phonePrefix,
            DisplayOrder = displayOrder,
            IsActive = true,
            IsInvited = request.IsInvited,
            DelegationArrivalDate = arrival,
            DelegationDepartureDate = departure,
            HeadOfDelegationUserProfileId = headId,
            CreatedAt = now,
        };

        appDbContext.Countries.Add(country);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.CountryCreated,
            actorUserId,
            $"id={id}; code={code}; name={name}",
            cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} created Country {Code} (id {Id})",
            actorUserId, code, id);

        return ToDetail(country);
    }

    public async Task<AdminCountryDetail> UpdateAsync(Guid actorUserId, int id, AdminUpdateCountryRequest request, CancellationToken cancellationToken = default)
    {
        var country = await appDbContext.Countries
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new ApiException(ErrorCodes.CountryNotFound, 404,
                "The country was not found.",
                "لم يتم العثور على البلد.");

        var (_, code, name, nameArabic, phonePrefix, displayOrder) = Validate(id, request.Code, request.Name, request.NameArabic, request.PhonePrefix, request.DisplayOrder);

        if (!string.Equals(country.Code, code, StringComparison.OrdinalIgnoreCase))
        {
            var clash = await appDbContext.Countries.AsNoTracking().AnyAsync(c => c.Id != id && c.Code == code, cancellationToken);
            if (clash)
            {
                throw new ApiException(ErrorCodes.CountryCodeDuplicate, 409,
                    $"A country with code '{code}' already exists.",
                    $"يوجد بلد بالرمز '{code}' بالفعل.");
            }
        }

        var (headId, arrival, departure) = NormalizeDelegation(request.IsInvited,
            request.HeadOfDelegationUserProfileId,
            request.DelegationArrivalDate, request.DelegationDepartureDate);
        await ValidateHeadAsync(id, headId, cancellationToken);

        country.Code = code;
        country.Name = name;
        country.NameArabic = nameArabic;
        country.PhonePrefix = phonePrefix;
        country.DisplayOrder = displayOrder;
        country.IsActive = request.IsActive;
        country.IsInvited = request.IsInvited;
        country.DelegationArrivalDate = arrival;
        country.DelegationDepartureDate = departure;
        country.HeadOfDelegationUserProfileId = headId;
        country.UpdatedAt = timeProvider.SimfNow();

        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.CountryUpdated,
            actorUserId,
            $"id={id}; code={code}; active={country.IsActive}",
            cancellationToken);

        return ToDetail(country);
    }

    public async Task DeactivateAsync(Guid actorUserId, int id, CancellationToken cancellationToken = default)
    {
        var country = await appDbContext.Countries
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new ApiException(ErrorCodes.CountryNotFound, 404,
                "The country was not found.",
                "لم يتم العثور على البلد.");

        if (!country.IsActive) { return; }

        country.IsActive = false;
        country.UpdatedAt = timeProvider.SimfNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.CountryDeactivated,
            actorUserId,
            $"id={id}; code={country.Code}",
            cancellationToken);
    }

    public async Task<IReadOnlyList<AdminCountryDelegateOption>> ListDelegatesAsync(
        int countryId, CancellationToken cancellationToken = default) =>
        await appDbContext.UserProfiles.AsNoTracking()
            .Where(profile => profile.IsActive
                && profile.IsDelegate
                && profile.NationalityId == countryId)
            .OrderBy(profile => profile.NameArabic)
            .ThenBy(profile => profile.Name)
            .Select(profile => new AdminCountryDelegateOption(
                profile.Id, profile.Name, profile.NameArabic, profile.JobTitle))
            .ToListAsync(cancellationToken);

    private static (int id, string code, string name, string nameArabic, string? phonePrefix, int displayOrder) Validate(int idRaw, string codeRaw, string nameRaw, string nameArabicRaw, string? phonePrefixRaw, int displayOrderRaw)
    {
        if (idRaw <= 0)
        {
            throw new ApiException(ErrorCodes.CountryInvalid, 400,
                "Country id must be a positive integer (ISO 3166-1 numeric).",
                "يجب أن يكون معرّف البلد عدداً صحيحاً موجباً (ISO 3166-1).");
        }
        var code = (codeRaw ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length != 2)
        {
            throw new ApiException(ErrorCodes.CountryInvalid, 400,
                "Country code must be exactly 2 characters (ISO 3166-1 alpha-2).",
                "يجب أن يتكون رمز البلد من حرفين بالضبط (ISO 3166-1 alpha-2).");
        }
        var name = (nameRaw ?? string.Empty).Trim();
        if (name.Length is < 1 or > 128)
        {
            throw new ApiException(ErrorCodes.CountryInvalid, 400,
                "Country English name must be between 1 and 128 characters.",
                "يجب أن يتراوح الاسم الإنجليزي للبلد بين 1 و 128 حرفاً.");
        }
        var nameArabic = (nameArabicRaw ?? string.Empty).Trim();
        if (nameArabic.Length is < 1 or > 128)
        {
            throw new ApiException(ErrorCodes.CountryInvalid, 400,
                "Country Arabic name must be between 1 and 128 characters.",
                "يجب أن يتراوح الاسم العربي للبلد بين 1 و 128 حرفاً.");
        }
        var phonePrefix = string.IsNullOrWhiteSpace(phonePrefixRaw)
            ? null : phonePrefixRaw.Trim();
        if (phonePrefix is { Length: > 8 })
        {
            throw new ApiException(ErrorCodes.CountryInvalid, 400,
                "Phone prefix must be 8 characters or fewer.",
                "يجب أن يكون مقدمة الهاتف 8 أحرف أو أقل.");
        }
        if (displayOrderRaw < 0)
        {
            throw new ApiException(ErrorCodes.CountryInvalid, 400,
                "Display order must be zero or a positive integer.",
                "يجب أن يكون ترتيب العرض صفراً أو عدداً صحيحاً موجباً.");
        }
        return (idRaw, code, name, nameArabic, phonePrefix, displayOrderRaw);
    }

    /// <summary>Delegation data (head + arrival/departure dates) is only
    /// meaningful for an invited country; this clears all three when the country
    /// is not invited so a non-invited row never carries orphaned data.</summary>
    private static (Guid? HeadId, DateOnly? Arrival, DateOnly? Departure) NormalizeDelegation(
        bool invited, Guid? headId, DateOnly? arrival, DateOnly? departure) =>
        invited ? (headId, arrival, departure) : (null, null, null);

    /// <summary>Guards the head-of-delegation pointer: when supplied it
    /// must reference an active delegate (<see cref="UserProfile.IsDelegate"/>)
    /// whose nationality is this country. Keeps a country from pointing at an
    /// arbitrary or non-delegate profile.</summary>
    private async Task ValidateHeadAsync(int countryId, Guid? headProfileId, CancellationToken cancellationToken)
    {
        if (headProfileId is not { } id)
        {
            return;
        }

        var isEligible = await appDbContext.UserProfiles.AsNoTracking().AnyAsync(
            profile => profile.Id == id
                && profile.IsActive
                && profile.IsDelegate
                && profile.NationalityId == countryId,
            cancellationToken);
        if (!isEligible)
        {
            throw new ApiException(ErrorCodes.CountryInvalid, 400,
                "The head of delegation must be an active delegate of this country.",
                "يجب أن يكون رئيس الوفد عضو وفد نشطاً من هذا البلد.");
        }
    }

    private static AdminCountryDetail ToDetail(Country country) =>
        new(country.Id, country.Code, country.Name, country.NameArabic,
            country.PhonePrefix, country.DisplayOrder,
            country.IsActive, country.CreatedAt, country.UpdatedAt, country.IsInvited,
            country.DelegationArrivalDate, country.DelegationDepartureDate,
            country.HeadOfDelegationUserProfileId);
}
