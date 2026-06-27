// Tests: SIMF.Api.Tests/ContactsTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Contacts.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Contacts;
using SIMF.Domain.Contacts;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Contacts;

/// <summary>
/// Shared <see cref="Contact"/> directory — admin CRUD (SIMF-FDS-014, D-261).
/// Built on <see cref="SimfAppDbContext"/>. Mirrors <c>AdminOrganisationService</c>:
/// bilingual (NameAr / NameEn), soft-delete (IsActive), audited via
/// <see cref="IAuditLog"/>. <c>CountryId</c> is a same-DB FK to <c>Country</c>,
/// resolved to bilingual names for the grid/detail in one batched lookup (no N+1).
/// A Contact still referenced by an active Company / Sponsor / MediaPartner /
/// Speaker / Booth cannot be deactivated (409 <see cref="ErrorCodes.ContactInUse"/>).
/// </summary>
internal sealed class AdminContactService(
    SimfAppDbContext db,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<AdminContactService> logger) : IAdminContactService
{
    public async Task<GridPage<AdminContactSummary>> ListAsync(
        GridQuery query, CancellationToken ct = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 25, 1, 200);

        var rows = db.Contacts.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(contact =>
                EF.Functions.Like(contact.NameArabic, $"%{term}%")
                || (contact.Name != null && EF.Functions.Like(contact.Name, $"%{term}%"))
                || (contact.Email != null && EF.Functions.Like(contact.Email, $"%{term}%"))
                || (contact.PhonePrimary != null && EF.Functions.Like(contact.PhonePrimary, $"%{term}%")));
        }

        foreach (var (column, raw) in query.Filters)
        {
            if (string.IsNullOrWhiteSpace(raw)) { continue; }
            var v = raw.Trim();
            switch (column.ToLowerInvariant())
            {
                case "name":
                    rows = rows.Where(contact => contact.NameArabic.Contains(v));
                    break;
                case "nameen":
                    rows = rows.Where(contact => contact.Name != null && contact.Name.Contains(v));
                    break;
                case "email":
                    rows = rows.Where(contact => contact.Email != null && contact.Email.Contains(v));
                    break;
                case "isactive":
                    if (bool.TryParse(v, out var isActive))
                    {
                        rows = rows.Where(contact => contact.IsActive == isActive);
                    }
                    break;
            }
        }

        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("name", true) => rows.OrderByDescending(contact => contact.NameArabic),
            ("name", false) => rows.OrderBy(contact => contact.NameArabic),
            ("isactive", true) => rows.OrderByDescending(contact => contact.IsActive),
            ("isactive", false) => rows.OrderBy(contact => contact.IsActive),
            _ => rows.OrderBy(contact => contact.NameArabic),
        };

        var total = await rows.CountAsync(ct);
        var pageRows = await rows
            .Skip(skip)
            .Take(top)
            .ToListAsync(ct);

        var countryNames = await ResolveCountryNamesAsync(
            pageRows.Select(contact => contact.CountryId), ct);

        var page = pageRows
            .Select(contact =>
            {
                var (countryEn, countryAr) = LookupCountry(countryNames, contact.CountryId);
                return new AdminContactSummary(
                    contact.Id,
                    contact.NameArabic,
                    contact.Name,
                    contact.PhonePrimary,
                    contact.Email,
                    contact.CountryId,
                    countryEn,
                    countryAr,
                    contact.IsActive);
            })
            .ToList();

        return GridPage<AdminContactSummary>.Of(page, total,
            new GridQuery { Skip = skip, Top = top });
    }

    public async Task<AdminContactDetail?> GetAsync(
        Guid id, CancellationToken ct = default)
    {
        var contact = await db.Contacts
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == id, ct);
        return contact is null ? null : await ToDetailAsync(contact, ct);
    }

    public async Task<AdminContactDetail> CreateAsync(
        Guid actorUserId,
        CreateContactRequest request,
        CancellationToken ct = default)
    {
        var v = ValidateAndNormalise(
            request.NameAr, request.NameEn, request.LogoRelativePath,
            request.PhonePrimary, request.PhoneSecondary, request.Email, request.Website,
            request.FacebookUrl, request.XUrl, request.LinkedInUrl, request.InstagramUrl,
            request.City, request.CityArabic,
            request.Latitude, request.Longitude);
        await EnsureCountryActiveAsync(request.CountryId, ct);

        var now = timeProvider.GetUtcNow();
        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            NameArabic = v.NameAr,
            Name = v.NameEn,
            LogoRelativePath = v.LogoRelativePath,
            PhonePrimary = v.PhonePrimary,
            PhoneSecondary = v.PhoneSecondary,
            Email = v.Email,
            Website = v.Website,
            FacebookUrl = v.FacebookUrl,
            XUrl = v.XUrl,
            LinkedInUrl = v.LinkedInUrl,
            InstagramUrl = v.InstagramUrl,
            City = v.City,
            CityArabic = v.CityArabic,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            CountryId = request.CountryId,
            IsActive = true,
            CreatedAt = now,
        };
        db.Contacts.Add(contact);
        await db.SaveChangesAsync(ct);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.ContactCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={contact.Id}; nameAr={v.NameAr}",
        }, ct);

        logger.LogInformation(
            "Admin {ActorId} created Contact {NameAr} ({Id})",
            actorUserId, v.NameAr, contact.Id);

        return await ToDetailAsync(contact, ct);
    }

    public async Task<AdminContactDetail> UpdateAsync(
        Guid actorUserId,
        Guid id,
        UpdateContactRequest request,
        CancellationToken ct = default)
    {
        var contact = await db.Contacts
            .SingleOrDefaultAsync(row => row.Id == id, ct)
            ?? throw NotFound();

        var v = ValidateAndNormalise(
            request.NameAr, request.NameEn, request.LogoRelativePath,
            request.PhonePrimary, request.PhoneSecondary, request.Email, request.Website,
            request.FacebookUrl, request.XUrl, request.LinkedInUrl, request.InstagramUrl,
            request.City, request.CityArabic,
            request.Latitude, request.Longitude);
        await EnsureCountryActiveAsync(request.CountryId, ct);

        // The referenced guard also covers the deactivation transition via PUT
        // (IsActive true → false), mirroring DeactivateAsync — otherwise an edit
        // could orphan an active link the DELETE path forbids. The reference
        // queries run only on an actual deactivation, not on every benign edit.
        if (!request.IsActive && contact.IsActive
            && await IsReferencedByActiveEntityAsync(id, ct))
        {
            throw ReferencedConflict();
        }

        contact.NameArabic = v.NameAr;
        contact.Name = v.NameEn;
        contact.LogoRelativePath = v.LogoRelativePath;
        contact.PhonePrimary = v.PhonePrimary;
        contact.PhoneSecondary = v.PhoneSecondary;
        contact.Email = v.Email;
        contact.Website = v.Website;
        contact.FacebookUrl = v.FacebookUrl;
        contact.XUrl = v.XUrl;
        contact.LinkedInUrl = v.LinkedInUrl;
        contact.InstagramUrl = v.InstagramUrl;
        contact.City = v.City;
        contact.CityArabic = v.CityArabic;
        contact.Latitude = request.Latitude;
        contact.Longitude = request.Longitude;
        contact.CountryId = request.CountryId;
        contact.IsActive = request.IsActive;
        contact.UpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.ContactUpdated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={contact.Id}; nameAr={v.NameAr}; active={contact.IsActive}",
        }, ct);

        return await ToDetailAsync(contact, ct);
    }

    public async Task DeactivateAsync(
        Guid actorUserId,
        Guid id,
        CancellationToken ct = default)
    {
        var contact = await db.Contacts
            .SingleOrDefaultAsync(row => row.Id == id, ct)
            ?? throw NotFound();

        if (!contact.IsActive)
        {
            return; // idempotent
        }

        // Referenced-delete guard: a Contact linked from an active entity must
        // not be deactivated (it would orphan the link).
        if (await IsReferencedByActiveEntityAsync(id, ct))
        {
            throw ReferencedConflict();
        }

        contact.Deactivate();
        contact.UpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.ContactDeactivated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={contact.Id}; nameAr={contact.NameArabic}",
        }, ct);
    }

    public async Task<IReadOnlyList<ContactPickerItem>> SearchPickerAsync(
        string? search, int top, CancellationToken ct = default)
    {
        var take = Math.Clamp(top is > 0 ? top : 20, 1, 50);
        var rows = db.Contacts.AsNoTracking().Where(contact => contact.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            rows = rows.Where(contact =>
                EF.Functions.Like(contact.NameArabic, $"%{term}%")
                || (contact.Name != null && EF.Functions.Like(contact.Name, $"%{term}%")));
        }

        return await rows
            .OrderBy(contact => contact.NameArabic)
            .Take(take)
            .Select(contact => new ContactPickerItem(
                contact.Id, contact.NameArabic, contact.Name, contact.LogoRelativePath))
            .ToListAsync(ct);
    }

    /// <summary>Resolves the bilingual names for a set of (possibly null) country
    /// ids in a single query, returning a lookup keyed by country id.</summary>
    private async Task<Dictionary<int, (string En, string Ar)>> ResolveCountryNamesAsync(
        IEnumerable<int?> countryIds, CancellationToken ct)
    {
        var ids = countryIds
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await db.Countries
            .AsNoTracking()
            .Where(country => ids.Contains(country.Id))
            .ToDictionaryAsync(
                country => country.Id,
                country => (country.Name, country.NameArabic),
                ct);
    }

    private static (string? En, string? Ar) LookupCountry(
        Dictionary<int, (string En, string Ar)> names, int? countryId)
    {
        if (countryId is int id && names.TryGetValue(id, out var found))
        {
            return (found.En, found.Ar);
        }
        return (null, null);
    }

    private async Task<AdminContactDetail> ToDetailAsync(Contact c, CancellationToken ct)
    {
        var names = await ResolveCountryNamesAsync([c.CountryId], ct);
        var (countryEn, countryAr) = LookupCountry(names, c.CountryId);
        return new AdminContactDetail(
            c.Id,
            c.NameArabic,
            c.Name,
            c.LogoRelativePath,
            c.PhonePrimary,
            c.PhoneSecondary,
            c.Email,
            c.Website,
            c.FacebookUrl,
            c.XUrl,
            c.LinkedInUrl,
            c.InstagramUrl,
            c.Latitude,
            c.Longitude,
            c.CountryId,
            countryEn,
            countryAr,
            c.IsActive,
            c.CreatedAt,
            c.UpdatedAt,
            c.City,
            c.CityArabic);
    }

    private async Task EnsureCountryActiveAsync(int? countryId, CancellationToken ct)
    {
        if (countryId is not int id)
        {
            return;
        }
        var ok = await db.Countries
            .AsNoTracking()
            .AnyAsync(country => country.Id == id && country.IsActive, ct);
        if (!ok)
        {
            throw new ApiException(
                ErrorCodes.ContactInvalid, 400,
                "The selected country does not exist or is inactive.",
                "الدولة المحددة غير موجودة أو غير مفعّلة.");
        }
    }

    private sealed record ContactDraft(
        string NameAr, string? NameEn, string? LogoRelativePath,
        string? PhonePrimary, string? PhoneSecondary, string? Email, string? Website,
        string? FacebookUrl, string? XUrl, string? LinkedInUrl, string? InstagramUrl,
        string? City, string? CityArabic);

    private static ContactDraft ValidateAndNormalise(
        string nameArRaw, string? nameEnRaw, string? logoRaw,
        string? phonePrimaryRaw, string? phoneSecondaryRaw, string? emailRaw, string? websiteRaw,
        string? facebookRaw, string? xRaw, string? linkedInRaw, string? instagramRaw,
        string? cityRaw, string? cityArabicRaw,
        double? latitude, double? longitude)
    {
        var nameAr = (nameArRaw ?? string.Empty).Trim();
        if (nameAr.Length is < 1 or > 256)
        {
            throw new ApiException(
                ErrorCodes.ContactInvalid, 400,
                "Contact Arabic name must be between 1 and 256 characters.",
                "يجب أن يتراوح طول الاسم العربي لجهة الاتصال بين 1 و 256 حرفاً.");
        }

        // Lengths mirror ContactConfiguration.HasMaxLength.
        var nameEn = OptionalText(nameEnRaw, 256, "Contact English name", "الاسم الإنجليزي لجهة الاتصال");
        var logo = OptionalText(logoRaw, 512, "Logo path", "مسار الشعار");
        var phonePrimary = OptionalText(phonePrimaryRaw, 32, "Primary phone", "الهاتف الأساسي");
        var phoneSecondary = OptionalText(phoneSecondaryRaw, 32, "Secondary phone", "الهاتف الثانوي");
        var email = OptionalText(emailRaw, 320, "Email", "البريد الإلكتروني");
        var website = OptionalText(websiteRaw, 512, "Website", "الموقع الإلكتروني");
        var facebook = OptionalText(facebookRaw, 256, "Facebook URL", "رابط فيسبوك");
        var x = OptionalText(xRaw, 256, "X URL", "رابط إكس");
        var linkedIn = OptionalText(linkedInRaw, 256, "LinkedIn URL", "رابط لينكدإن");
        var instagram = OptionalText(instagramRaw, 256, "Instagram URL", "رابط إنستغرام");
        var city = OptionalText(cityRaw, 128, "City", "المدينة");
        var cityArabic = OptionalText(cityArabicRaw, 128, "Arabic city", "المدينة بالعربية");

        // Map coordinates are set together, and within WGS84 bounds.
        if (latitude.HasValue != longitude.HasValue)
        {
            throw new ApiException(
                ErrorCodes.ContactInvalid, 400,
                "Latitude and longitude must be set together.",
                "يجب تحديد خط العرض وخط الطول معاً.");
        }
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            throw new ApiException(
                ErrorCodes.ContactInvalid, 400,
                "Latitude must be between -90 and 90 and longitude between -180 and 180.",
                "يجب أن يكون خط العرض بين -90 و 90 وخط الطول بين -180 و 180.");
        }

        return new ContactDraft(
            nameAr, nameEn, logo, phonePrimary, phoneSecondary, email, website,
            facebook, x, linkedIn, instagram, city, cityArabic);
    }

    private static string? OptionalText(string? raw, int maxLength, string fieldEn, string fieldAr)
    {
        var value = NullIfBlank(raw);
        if (value is not null && value.Length > maxLength)
        {
            throw new ApiException(
                ErrorCodes.ContactInvalid, 400,
                $"{fieldEn} must be {maxLength} characters or fewer.",
                $"يجب ألا يتجاوز {fieldAr} {maxLength} حرفاً.");
        }
        return value;
    }

    /// <summary>True when the contact is still linked from an active Company /
    /// Sponsor / MediaPartner / Speaker / Booth. The OR short-circuits, so a hit
    /// on an early referrer skips the rest.</summary>
    private async Task<bool> IsReferencedByActiveEntityAsync(Guid id, CancellationToken ct) =>
        await db.Exhibitors.AsNoTracking().AnyAsync(c => c.IsActive && c.ContactId == id, ct)
        || await db.Sponsors.AsNoTracking().AnyAsync(s => s.IsActive && s.ContactId == id, ct)
        || await db.MediaPartners.AsNoTracking().AnyAsync(m => m.IsActive && m.ContactId == id, ct)
        || await db.Speakers.AsNoTracking().AnyAsync(sp => sp.IsActive && sp.ContactId == id, ct)
        || await db.Booths.AsNoTracking().AnyAsync(b => b.IsActive && b.ContactId == id, ct);

    private static ApiException ReferencedConflict() =>
        new(
            ErrorCodes.ContactInUse, 409,
            "This contact is still linked to an active company, sponsor, media partner, speaker or booth and cannot be deactivated.",
            "ما زالت جهة الاتصال هذه مرتبطة بشركة أو راعٍ أو شريك إعلامي أو متحدث أو جناح نشط، ولا يمكن إلغاء تفعيلها.");

    private static ApiException NotFound() =>
        new(
            ErrorCodes.ContactNotFound, 404,
            "The contact was not found.",
            "لم يتم العثور على جهة الاتصال.");

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
