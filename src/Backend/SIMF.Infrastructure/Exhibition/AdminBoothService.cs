// Tests: SIMF.Api.Tests/AdminBoothsTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Assets.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.Exhibition.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Exhibition;
using SIMF.Domain.Exhibition;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Exhibition;

/// <summary>
/// D-199 — admin CRUD over <see cref="Booth"/> (Exhibition module, Mockup
/// page 22 + the 2D venue map). Built on <see cref="SimfAppDbContext"/>.
/// Mirrors <c>AdminSpeakerService</c>: bilingual (Name/NameArabic), unique
/// Code (409 on duplicate), soft-delete (IsActive), audited via
/// <see cref="IAuditLog"/>. <c>HallId</c> is validated against the live
/// <c>Halls</c> table (same context) when supplied.
/// </summary>
internal sealed class AdminBoothService(
    SimfAppDbContext dbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    IAssetService assetService,
    ILogger<AdminBoothService> logger) : IAdminBoothService
{
    public async Task<GridPage<AdminBoothSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var (skip, top) = query.ClampPage(25, 200);

        var rows = dbContext.Booths.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(booth =>
                EF.Functions.Like(booth.Code, $"%{term}%")
                || EF.Functions.Like(booth.Name, $"%{term}%")
                || EF.Functions.Like(booth.NameArabic, $"%{term}%"));
        }

        // CP grid per-column filters (D-255). Unknown columns are ignored.
        // The Exhibitor + Hall columns are resolved client-side from cached
        // lookups (the summary carries only the ids), so they are NOT
        // server-filterable and are intentionally absent here.
        foreach (var (column, raw) in query.Filters)
        {
            if (string.IsNullOrWhiteSpace(raw)) { continue; }
            var v = raw.Trim();
            switch (column.ToLowerInvariant())
            {
                case "code":
                    rows = rows.Where(booth => booth.Code.Contains(v));
                    break;
                case "name":
                    rows = rows.Where(booth => booth.Name.Contains(v));
                    break;
                case "namearabic":
                    rows = rows.Where(booth => booth.NameArabic.Contains(v));
                    break;
                case "sector":
                    rows = rows.Where(booth => booth.Sector != null && booth.Sector.Contains(v));
                    break;
            }
        }

        // CP grid sortable columns (D-255). Default: Code. The Exhibitor + Hall
        // columns sort on a client-resolved value, so they are not server-
        // sortable and are absent here.
        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("code", true) => rows.OrderByDescending(booth => booth.Code),
            ("code", false) => rows.OrderBy(booth => booth.Code),
            ("name", true) => rows.OrderByDescending(booth => booth.Name),
            ("name", false) => rows.OrderBy(booth => booth.Name),
            ("sector", true) => rows.OrderByDescending(booth => booth.Sector),
            ("sector", false) => rows.OrderBy(booth => booth.Sector),
            ("isactive", true) => rows.OrderByDescending(booth => booth.IsActive),
            ("isactive", false) => rows.OrderBy(booth => booth.IsActive),
            _ => rows.OrderBy(booth => booth.Code),
        };

        var total = await rows.CountAsync(cancellationToken);
        var pageRows = await rows
            .Skip(skip)
            .Take(top)
            .Select(booth => new
            {
                booth.Id,
                booth.Code,
                booth.Name,
                booth.NameArabic,
                booth.ExhibitorId,
                booth.Sector,
                booth.HallId,
                booth.IsActive,
            })
            .ToListAsync(cancellationToken);

        // The booth owns its own BoothLogo (the app renders this) — one batched
        // query over the page's booth ids, no N+1.
        var boothLogoOwners = await assetService.WhichOwnersHaveActiveAssetAsync(
            AssetCategory.BoothLogo, pageRows.Select(row => row.Id).ToList(), cancellationToken);

        var page = pageRows
            .Select(booth => new AdminBoothSummary
            {
                Id = booth.Id,
                Code = booth.Code,
                Name = booth.Name,
                NameArabic = booth.NameArabic,
                ExhibitorId = booth.ExhibitorId,
                Sector = booth.Sector,
                HallId = booth.HallId,
                IsActive = booth.IsActive,
                // Append-only frozen wire field; the shared Contact directory was
                // removed, so it now always emits null (and no CompanyLogo lookup).
                ExhibitorContactId = null,
                HasLogo = false,
                HasBoothLogo = boothLogoOwners.Contains(booth.Id),
            })
            .ToList();

        return GridPage<AdminBoothSummary>.Of(page, total,
            skip, top);
    }

    public async Task<AdminBoothDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        // D-673 — pull the linked exhibitor so the detail can surface the
        // exhibitor-owned Website / City / Tier (the fields the app booth detail
        // shows; City is now inlined on the Exhibitor). AsNoTracking → LEFT JOIN,
        // single row. The create/update paths do not load this navigation, so
        // their ToDetail echo leaves the resolved fields null (by design).
        var booth = await dbContext.Booths
            .AsNoTracking()
            .Include(row => row.Exhibitor)
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        if (booth is null) { return null; }
        var (officerCountryEn, officerCountryAr) =
            await ResolveOfficerCountryAsync(booth.OfficerCountryId, cancellationToken);
        return ToDetail(booth, officerCountryEn, officerCountryAr);
    }

    public async Task<AdminBoothDetail> CreateAsync(
        Guid actorUserId,
        AdminCreateBoothRequest request,
        CancellationToken cancellationToken = default)
    {
        var v = ValidateAndNormalise(
            request.Code, request.Name, request.NameArabic,
            request.OfficerName, request.OfficerPhone, request.OfficerEmail,
            request.Sector, request.SectorArabic,
            request.Description, request.DescriptionArabic);
        ValidateContactFields(
            request.OfficerNameArabic, request.OfficerPhoneSecondary,
            request.OfficerWebsite, request.OfficerFacebookUrl, request.OfficerXUrl,
            request.OfficerLinkedInUrl, request.OfficerInstagramUrl,
            request.OfficerCity, request.OfficerCityArabic,
            request.OfficerLatitude, request.OfficerLongitude);
        await EnsureHallIsValidAsync(request.HallId, cancellationToken);
        await EnsureExhibitorIsValidAsync(request.ExhibitorId, cancellationToken);
        await EnsureOfficerCountryIsValidAsync(request.OfficerCountryId, cancellationToken);

        var clash = await dbContext.Booths
            .AsNoTracking()
            .AnyAsync(row => row.Code == v.Code, cancellationToken);
        if (clash)
        {
            throw new ApiException(
                ErrorCodes.BoothCodeDuplicate, 409,
                $"A booth with code '{v.Code}' already exists.",
                $"يوجد جناح بالرمز '{v.Code}' بالفعل.");
        }

        var now = timeProvider.SimfNow();
        var booth = new Booth
        {
            Id = Guid.NewGuid(),
            Code = v.Code,
            Name = v.Name,
            NameArabic = v.NameArabic,
            ExhibitorId = request.ExhibitorId,
            OfficerName = v.OfficerName,
            OfficerPhone = v.OfficerPhone,
            OfficerEmail = v.OfficerEmail,
            OfficerNameArabic = NullIfBlank(request.OfficerNameArabic),
            OfficerPhoneSecondary = NullIfBlank(request.OfficerPhoneSecondary),
            OfficerWebsite = NullIfBlank(request.OfficerWebsite),
            OfficerFacebookUrl = NullIfBlank(request.OfficerFacebookUrl),
            OfficerXUrl = NullIfBlank(request.OfficerXUrl),
            OfficerLinkedInUrl = NullIfBlank(request.OfficerLinkedInUrl),
            OfficerInstagramUrl = NullIfBlank(request.OfficerInstagramUrl),
            OfficerCity = NullIfBlank(request.OfficerCity),
            OfficerCityArabic = NullIfBlank(request.OfficerCityArabic),
            OfficerLatitude = request.OfficerLatitude,
            OfficerLongitude = request.OfficerLongitude,
            OfficerCountryId = request.OfficerCountryId,
            Sector = v.Sector,
            SectorArabic = v.SectorArabic,
            Description = v.Description,
            DescriptionArabic = v.DescriptionArabic,
            HallId = request.HallId,
            MapX = request.MapX,
            MapY = request.MapY,
            IsActive = true,
            CreatedAt = now,
        };
        dbContext.Booths.Add(booth);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.BoothCreated,
            actorUserId,
            $"id={booth.Id}; code={v.Code}; name={v.Name}",
            cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} created Booth {Code} ({Id})",
            actorUserId, v.Code, booth.Id);

        var (officerCountryEn, officerCountryAr) =
            await ResolveOfficerCountryAsync(booth.OfficerCountryId, cancellationToken);
        return ToDetail(booth, officerCountryEn, officerCountryAr);
    }

    public async Task<AdminBoothDetail> UpdateAsync(
        Guid actorUserId,
        Guid id,
        AdminUpdateBoothRequest request,
        CancellationToken cancellationToken = default)
    {
        var booth = await dbContext.Booths
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.BoothNotFound, 404,
                "The booth was not found.",
                "لم يتم العثور على الجناح.");

        var v = ValidateAndNormalise(
            request.Code, request.Name, request.NameArabic,
            request.OfficerName, request.OfficerPhone, request.OfficerEmail,
            request.Sector, request.SectorArabic,
            request.Description, request.DescriptionArabic);
        ValidateContactFields(
            request.OfficerNameArabic, request.OfficerPhoneSecondary,
            request.OfficerWebsite, request.OfficerFacebookUrl, request.OfficerXUrl,
            request.OfficerLinkedInUrl, request.OfficerInstagramUrl,
            request.OfficerCity, request.OfficerCityArabic,
            request.OfficerLatitude, request.OfficerLongitude);
        await EnsureHallIsValidAsync(request.HallId, cancellationToken);
        await EnsureExhibitorIsValidAsync(request.ExhibitorId, cancellationToken);
        await EnsureOfficerCountryIsValidAsync(request.OfficerCountryId, cancellationToken);

        if (!string.Equals(booth.Code, v.Code, StringComparison.OrdinalIgnoreCase))
        {
            var clash = await dbContext.Booths
                .AsNoTracking()
                .AnyAsync(row => row.Id != id && row.Code == v.Code, cancellationToken);
            if (clash)
            {
                throw new ApiException(
                    ErrorCodes.BoothCodeDuplicate, 409,
                    $"A booth with code '{v.Code}' already exists.",
                    $"يوجد جناح بالرمز '{v.Code}' بالفعل.");
            }
        }

        booth.Code = v.Code;
        booth.Name = v.Name;
        booth.NameArabic = v.NameArabic;
        booth.ExhibitorId = request.ExhibitorId;
        booth.OfficerName = v.OfficerName;
        booth.OfficerPhone = v.OfficerPhone;
        booth.OfficerEmail = v.OfficerEmail;
        booth.OfficerNameArabic = NullIfBlank(request.OfficerNameArabic);
        booth.OfficerPhoneSecondary = NullIfBlank(request.OfficerPhoneSecondary);
        booth.OfficerWebsite = NullIfBlank(request.OfficerWebsite);
        booth.OfficerFacebookUrl = NullIfBlank(request.OfficerFacebookUrl);
        booth.OfficerXUrl = NullIfBlank(request.OfficerXUrl);
        booth.OfficerLinkedInUrl = NullIfBlank(request.OfficerLinkedInUrl);
        booth.OfficerInstagramUrl = NullIfBlank(request.OfficerInstagramUrl);
        booth.OfficerCity = NullIfBlank(request.OfficerCity);
        booth.OfficerCityArabic = NullIfBlank(request.OfficerCityArabic);
        booth.OfficerLatitude = request.OfficerLatitude;
        booth.OfficerLongitude = request.OfficerLongitude;
        booth.OfficerCountryId = request.OfficerCountryId;
        booth.Sector = v.Sector;
        booth.SectorArabic = v.SectorArabic;
        booth.Description = v.Description;
        booth.DescriptionArabic = v.DescriptionArabic;
        booth.HallId = request.HallId;
        booth.MapX = request.MapX;
        booth.MapY = request.MapY;
        booth.IsActive = request.IsActive;
        booth.UpdatedAt = timeProvider.SimfNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.BoothUpdated,
            actorUserId,
            $"id={booth.Id}; code={v.Code}; active={booth.IsActive}",
            cancellationToken);

        var (officerCountryEn, officerCountryAr) =
            await ResolveOfficerCountryAsync(booth.OfficerCountryId, cancellationToken);
        return ToDetail(booth, officerCountryEn, officerCountryAr);
    }

    public async Task DeactivateAsync(
        Guid actorUserId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var booth = await dbContext.Booths
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.BoothNotFound, 404,
                "The booth was not found.",
                "لم يتم العثور على الجناح.");

        if (!booth.IsActive)
        {
            return; // idempotent
        }

        // #26 — a soft-deleted booth would orphan any venue-map node that marks it
        // (the VenueMapNode→Booth FK is Restrict, but that only guards a hard delete,
        // not a soft delete). Block with a clear 409 so the admin removes the map
        // node first — mirrors AdminContactService's ContactInUse guard.
        var markedOnVenueMap = await dbContext.VenueMapNodes
            .AsNoTracking()
            .AnyAsync(node => node.IsActive && node.BoothId == id, cancellationToken);
        if (markedOnVenueMap)
        {
            throw new ApiException(
                ErrorCodes.BoothInUse, 409,
                "This booth is still marked on the venue map and cannot be deactivated. Remove its venue-map node first.",
                "ما زال هذا الجناح محدداً على خريطة المكان ولا يمكن إلغاء تفعيله. احذف عقدة الخريطة المرتبطة به أولاً.");
        }

        booth.Deactivate();
        booth.UpdatedAt = timeProvider.SimfNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.BoothDeactivated,
            actorUserId,
            $"id={booth.Id}; code={booth.Code}",
            cancellationToken);
    }

    private sealed record BoothDraft(
        string Code, string Name, string NameArabic,
        string? OfficerName, string? OfficerPhone, string? OfficerEmail,
        string? Sector, string? SectorArabic,
        string? Description, string? DescriptionArabic);

    private static BoothDraft ValidateAndNormalise(
        string codeRaw, string nameRaw, string nameArabicRaw,
        string? officerNameRaw, string? officerPhoneRaw, string? officerEmailRaw,
        string? sectorRaw, string? sectorArabicRaw,
        string? descriptionRaw, string? descriptionArabicRaw)
    {
        var code = (codeRaw ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length is < 2 or > 16)
        {
            throw new ApiException(
                ErrorCodes.BoothInvalid, 400,
                "Booth code must be between 2 and 16 characters.",
                "يجب أن يتراوح طول رمز الجناح بين 2 و 16 حرفاً.");
        }
        var name = (nameRaw ?? string.Empty).Trim();
        if (name.Length is < 1 or > 128)
        {
            throw new ApiException(
                ErrorCodes.BoothInvalid, 400,
                "Booth English name must be between 1 and 128 characters.",
                "يجب أن يتراوح طول الاسم الإنجليزي للجناح بين 1 و 128 حرفاً.");
        }
        var nameArabic = (nameArabicRaw ?? string.Empty).Trim();
        if (nameArabic.Length is < 1 or > 128)
        {
            throw new ApiException(
                ErrorCodes.BoothInvalid, 400,
                "Booth Arabic name must be between 1 and 128 characters.",
                "يجب أن يتراوح طول الاسم العربي للجناح بين 1 و 128 حرفاً.");
        }

        // Optional fields — lengths mirror BoothConfiguration.HasMaxLength
        // (Officer name = 256 / phone = 32 / email = 320, Sector* = 128,
        // Description* = 2048). B1 — D-222: booth-officer contact.
        var officerName = OptionalText(
            officerNameRaw, 256, "Booth officer name", "اسم مسؤول الجناح");
        var officerPhone = OptionalText(
            officerPhoneRaw, 32, "Booth officer phone", "هاتف مسؤول الجناح");
        var officerEmail = OptionalText(
            officerEmailRaw, 320, "Booth officer email", "بريد مسؤول الجناح");
        if (officerEmail is not null && !officerEmail.Contains('@'))
        {
            throw new ApiException(
                ErrorCodes.BoothInvalid, 400,
                "Booth officer email is not a valid email address.",
                "بريد مسؤول الجناح غير صالح.");
        }
        var sector = OptionalText(
            sectorRaw, 128, "Booth English sector", "قطاع الجناح الإنجليزي");
        var sectorArabic = OptionalText(
            sectorArabicRaw, 128, "Booth Arabic sector", "قطاع الجناح العربي");
        var description = OptionalText(
            descriptionRaw, 2048, "Booth English description", "وصف الجناح الإنجليزي");
        var descriptionArabic = OptionalText(
            descriptionArabicRaw, 2048, "Booth Arabic description", "وصف الجناح العربي");

        return new BoothDraft(
            code, name, nameArabic,
            officerName, officerPhone, officerEmail,
            sector, sectorArabic,
            description, descriptionArabic);
    }

    private static string? OptionalText(string? raw, int maxLength, string fieldEn, string fieldAr)
    {
        var value = NullIfBlank(raw);
        if (value is not null && value.Length > maxLength)
        {
            throw new ApiException(
                ErrorCodes.BoothInvalid, 400,
                $"{fieldEn} must be {maxLength} characters or fewer.",
                $"يجب ألا يتجاوز {fieldAr} {maxLength} حرفاً.");
        }
        return value;
    }

    // D-766 — validates the NEW inline booth-officer identity-card fields (the
    // shared Contact directory was removed). Lengths mirror the EF configuration;
    // latitude and longitude are an all-or-nothing pair with real-world ranges.
    // OfficerName / OfficerPhone / OfficerEmail are validated in
    // ValidateAndNormalise above and are not re-checked here.
    private static void ValidateContactFields(
        string? nameArabic, string? phoneSecondary,
        string? website, string? facebook, string? x, string? linkedIn,
        string? instagram, string? city, string? cityArabic,
        double? latitude, double? longitude)
    {
        if (!string.IsNullOrWhiteSpace(nameArabic) && nameArabic.Length > 256)
        {
            throw Invalid("Booth officer Arabic name must be 256 characters or fewer.",
                "يجب ألا يتجاوز الاسم العربي لمسؤول الجناح 256 حرفاً.");
        }
        if (!string.IsNullOrWhiteSpace(phoneSecondary) && phoneSecondary.Length > 32)
        {
            throw Invalid("Phone numbers must be 32 characters or fewer.",
                "يجب ألا يتجاوز رقم الهاتف 32 حرفاً.");
        }
        if (!string.IsNullOrWhiteSpace(website) && website.Length > 512)
        {
            throw Invalid("Website URL must be 512 characters or fewer.",
                "يجب ألا يتجاوز رابط الموقع الإلكتروني 512 حرفاً.");
        }
        foreach (var url in new[] { facebook, x, linkedIn, instagram })
        {
            if (!string.IsNullOrWhiteSpace(url) && url.Length > 256)
            {
                throw Invalid("Social URLs must be 256 characters or fewer.",
                    "يجب ألا يتجاوز رابط الشبكات الاجتماعية 256 حرفاً.");
            }
        }
        foreach (var cityValue in new[] { city, cityArabic })
        {
            if (!string.IsNullOrWhiteSpace(cityValue) && cityValue.Length > 128)
            {
                throw Invalid("City must be 128 characters or fewer.",
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
        new(ErrorCodes.BoothInvalid, 400, english, arabic);

    private async Task EnsureHallIsValidAsync(
        Guid? hallId, CancellationToken cancellationToken)
    {
        if (hallId is null) { return; }
        var exists = await dbContext.Halls
            .AsNoTracking()
            .AnyAsync(hall => hall.Id == hallId.Value && hall.IsActive, cancellationToken);
        if (!exists)
        {
            throw new ApiException(
                ErrorCodes.BoothInvalid, 400,
                $"Hall id '{hallId}' does not exist or is inactive.",
                $"رقم القاعة '{hallId}' غير موجود أو غير مفعّل.");
        }
    }

    // B1 — D-222: the exhibitor must be an active Exhibitor row. Mirrors
    // EnsureHallIsValidAsync. Inactive exhibitors are rejected so a booth never
    // points at a soft-deleted row.
    private async Task EnsureExhibitorIsValidAsync(
        Guid? exhibitorId, CancellationToken cancellationToken)
    {
        if (exhibitorId is null) { return; }
        var exists = await dbContext.Exhibitors
            .AsNoTracking()
            .AnyAsync(exhibitor => exhibitor.Id == exhibitorId.Value
                && exhibitor.IsActive, cancellationToken);
        if (!exists)
        {
            throw new ApiException(
                ErrorCodes.BoothInvalid, 400,
                $"Exhibitor id '{exhibitorId}' is not an active exhibitor.",
                $"معرّف العارض '{exhibitorId}' ليس عارضاً مفعّلاً.");
        }
    }

    // D-766 — the booth officer's country is a logical FK to the live Country
    // table (same App context). Mirrors AdminSpeakerService.EnsureCountryIsValid.
    private async Task EnsureOfficerCountryIsValidAsync(
        int? countryId, CancellationToken cancellationToken)
    {
        if (countryId is null) { return; }
        var exists = await dbContext.Countries
            .AsNoTracking()
            .AnyAsync(country => country.Id == countryId.Value && country.IsActive, cancellationToken);
        if (!exists)
        {
            throw new ApiException(
                ErrorCodes.BoothInvalid, 400,
                $"Country id '{countryId}' does not exist or is inactive.",
                $"رقم البلد '{countryId}' غير موجود أو غير مفعّل.");
        }
    }

    private async Task<(string? en, string? ar)> ResolveOfficerCountryAsync(
        int? countryId, CancellationToken cancellationToken)
    {
        if (countryId is null) { return (null, null); }
        var row = await dbContext.Countries
            .AsNoTracking()
            .Where(country => country.Id == countryId.Value)
            .Select(country => new { country.Name, country.NameArabic })
            .SingleOrDefaultAsync(cancellationToken);
        return (row?.Name, row?.NameArabic);
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static AdminBoothDetail ToDetail(
        Booth b, string? officerCountryNameEn, string? officerCountryNameAr) => new()
    {
        Id = b.Id,
        Code = b.Code,
        Name = b.Name,
        NameArabic = b.NameArabic,
        ExhibitorId = b.ExhibitorId,
        OfficerName = b.OfficerName,
        OfficerPhone = b.OfficerPhone,
        OfficerEmail = b.OfficerEmail,
        OfficerNameArabic = b.OfficerNameArabic,
        OfficerPhoneSecondary = b.OfficerPhoneSecondary,
        OfficerWebsite = b.OfficerWebsite,
        OfficerFacebookUrl = b.OfficerFacebookUrl,
        OfficerXUrl = b.OfficerXUrl,
        OfficerLinkedInUrl = b.OfficerLinkedInUrl,
        OfficerInstagramUrl = b.OfficerInstagramUrl,
        OfficerCity = b.OfficerCity,
        OfficerCityArabic = b.OfficerCityArabic,
        OfficerLatitude = b.OfficerLatitude,
        OfficerLongitude = b.OfficerLongitude,
        OfficerCountryId = b.OfficerCountryId,
        OfficerCountryNameEn = officerCountryNameEn,
        OfficerCountryNameAr = officerCountryNameAr,
        Sector = b.Sector,
        SectorArabic = b.SectorArabic,
        Description = b.Description,
        DescriptionArabic = b.DescriptionArabic,
        HallId = b.HallId,
        MapX = b.MapX,
        MapY = b.MapY,
        IsActive = b.IsActive,
        // D-673 — read-only exhibitor-resolved fields (mirrors PublicBoothService):
        // Website + Tier from the linked Exhibitor, City/CityArabic now inlined on
        // the Exhibitor. Null when the Exhibitor navigation was not loaded (create /
        // update paths) or the booth has no linked exhibitor. ExhibitorContactId is
        // an append-only frozen wire field that now always emits null (the shared
        // Contact directory was removed).
        Website = b.Exhibitor?.Website,
        City = b.Exhibitor?.City,
        CityArabic = b.Exhibitor?.CityArabic,
        Tier = (int?)b.Exhibitor?.Tier,
        TierName = b.Exhibitor?.Tier?.ToString(),
        ExhibitorContactId = null,
    };
}
