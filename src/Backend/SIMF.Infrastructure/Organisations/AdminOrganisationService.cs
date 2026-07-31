// Tests: SIMF.Api.Tests/OrganisationTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Organisations.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Organisations;
using SIMF.Domain.Organisations;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Organisations;

/// <summary>
/// Organisation lookup — admin CRUD + bulk Excel import over
/// <see cref="Organisation"/> (bilingual Saudi-companies directory). Built on
/// <see cref="SimfAppDbContext"/>. Mirrors <c>AdminBoothService</c>: bilingual
/// (NameAr / NameEn), unique <c>CommercialRegistration</c> when present (409 on
/// duplicate), soft-delete (IsActive), audited via <see cref="IAuditLog"/>. The
/// gov Excel sheet is parsed by <see cref="IOrganisationExcelReader"/> and
/// up-serted: keyed on commercial registration when present, otherwise on the
/// exact active Arabic name.
/// </summary>
internal sealed class AdminOrganisationService(
    SimfAppDbContext db,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    IOrganisationExcelReader excelReader,
    ILogger<AdminOrganisationService> logger) : IAdminOrganisationService
{
    /// <summary>The most rows a single import flushes per <c>SaveChanges</c>.</summary>
    private const int ImportBatchSize = 500;

    /// <summary>The most per-row error messages the result carries back.</summary>
    private const int ImportErrorCap = 50;

    public async Task<GridPage<AdminOrganisationSummary>> ListAsync(
        GridQuery query, CancellationToken ct = default)
    {
        var (skip, top) = query.ClampPage(25, 200);

        var rows = db.Organisations.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(org =>
                EF.Functions.Like(org.NameArabic, $"%{term}%")
                || EF.Functions.Like(org.Name, $"%{term}%")
                || EF.Functions.Like(org.CommercialRegistration, $"%{term}%")
                || EF.Functions.Like(org.City, $"%{term}%"));
        }

        // CP grid per-column filters (D-255). Unknown columns are ignored; the
        // boolean isActive filter is parsed from its text value.
        foreach (var (column, raw) in query.Filters)
        {
            if (string.IsNullOrWhiteSpace(raw)) { continue; }
            var v = raw.Trim();
            switch (column.ToLowerInvariant())
            {
                case "name":
                    rows = rows.Where(org => org.NameArabic.Contains(v));
                    break;
                case "nameen":
                    rows = rows.Where(org => org.Name != null && org.Name.Contains(v));
                    break;
                case "commercialregistration":
                    rows = rows.Where(org =>
                        org.CommercialRegistration != null && org.CommercialRegistration.Contains(v));
                    break;
                case "sector":
                    rows = rows.Where(org => org.Sector != null && org.Sector.Contains(v));
                    break;
                case "city":
                    rows = rows.Where(org => org.City != null && org.City.Contains(v));
                    break;
                case "isactive":
                    if (bool.TryParse(v, out var isActive))
                    {
                        rows = rows.Where(org => org.IsActive == isActive);
                    }
                    break;
            }
        }

        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("name", true) => rows.OrderByDescending(org => org.NameArabic),
            ("name", false) => rows.OrderBy(org => org.NameArabic),
            ("city", true) => rows.OrderByDescending(org => org.City),
            ("city", false) => rows.OrderBy(org => org.City),
            ("isactive", true) => rows.OrderByDescending(org => org.IsActive),
            ("isactive", false) => rows.OrderBy(org => org.IsActive),
            _ => rows.OrderBy(org => org.NameArabic),
        };

        var total = await rows.CountAsync(ct);
        var page = await rows
            .Skip(skip)
            .Take(top)
            .Select(org => new AdminOrganisationSummary(
                org.Id,
                org.NameArabic,
                org.Name,
                org.CommercialRegistration,
                org.Sector,
                org.City,
                org.IsActive))
            .ToListAsync(ct);

        return GridPage<AdminOrganisationSummary>.Of(page, total,
            skip, top);
    }

    public async Task<AdminOrganisationDetail?> GetAsync(
        Guid id, CancellationToken ct = default)
    {
        var org = await db.Organisations
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == id, ct);
        return org is null ? null : ToDetail(org);
    }

    public async Task<AdminOrganisationDetail> CreateAsync(
        Guid actorUserId,
        CreateOrganisationRequest request,
        CancellationToken ct = default)
    {
        var v = ValidateAndNormalise(
            request.NameAr, request.NameEn, request.CommercialRegistration,
            request.Sector, request.City, request.Phone, request.Email, request.Website);

        if (v.CommercialRegistration is not null)
        {
            var clash = await db.Organisations
                .AsNoTracking()
                .AnyAsync(row => row.CommercialRegistration == v.CommercialRegistration, ct);
            if (clash)
            {
                throw DuplicateCommercialRegistration(v.CommercialRegistration);
            }
        }

        var now = timeProvider.SimfNow();
        var org = new Organisation
        {
            Id = Guid.NewGuid(),
            NameArabic = v.NameAr,
            Name = v.NameEn,
            CommercialRegistration = v.CommercialRegistration,
            Sector = v.Sector,
            City = v.City,
            Phone = v.Phone,
            Email = v.Email,
            Website = v.Website,
            IsActive = true,
            CreatedAt = now,
        };
        db.Organisations.Add(org);
        await db.SaveChangesAsync(ct);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.OrganisationCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={org.Id}; nameAr={v.NameAr}; cr={v.CommercialRegistration}",
        }, ct);

        logger.LogInformation(
            "Admin {ActorId} created Organisation {NameAr} ({Id})",
            actorUserId, v.NameAr, org.Id);

        return ToDetail(org);
    }

    public async Task<AdminOrganisationDetail> UpdateAsync(
        Guid actorUserId,
        Guid id,
        UpdateOrganisationRequest request,
        CancellationToken ct = default)
    {
        var org = await db.Organisations
            .SingleOrDefaultAsync(row => row.Id == id, ct)
            ?? throw NotFound();

        var v = ValidateAndNormalise(
            request.NameAr, request.NameEn, request.CommercialRegistration,
            request.Sector, request.City, request.Phone, request.Email, request.Website);

        if (v.CommercialRegistration is not null
            && !string.Equals(org.CommercialRegistration, v.CommercialRegistration, StringComparison.OrdinalIgnoreCase))
        {
            var clash = await db.Organisations
                .AsNoTracking()
                .AnyAsync(row => row.Id != id && row.CommercialRegistration == v.CommercialRegistration, ct);
            if (clash)
            {
                throw DuplicateCommercialRegistration(v.CommercialRegistration);
            }
        }

        org.NameArabic = v.NameAr;
        org.Name = v.NameEn;
        org.CommercialRegistration = v.CommercialRegistration;
        org.Sector = v.Sector;
        org.City = v.City;
        org.Phone = v.Phone;
        org.Email = v.Email;
        org.Website = v.Website;
        org.IsActive = request.IsActive;
        org.UpdatedAt = timeProvider.SimfNow();
        await db.SaveChangesAsync(ct);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.OrganisationUpdated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={org.Id}; nameAr={v.NameAr}; active={org.IsActive}",
        }, ct);

        return ToDetail(org);
    }

    public async Task DeactivateAsync(
        Guid actorUserId,
        Guid id,
        CancellationToken ct = default)
    {
        var org = await db.Organisations
            .SingleOrDefaultAsync(row => row.Id == id, ct)
            ?? throw NotFound();

        if (!org.IsActive)
        {
            return; // idempotent
        }

        org.Deactivate();
        org.UpdatedAt = timeProvider.SimfNow();
        await db.SaveChangesAsync(ct);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.OrganisationDeactivated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={org.Id}; nameAr={org.NameArabic}",
        }, ct);
    }

    public async Task<OrganisationImportResult> ImportAsync(
        Guid actorUserId,
        Stream xlsxStream,
        CancellationToken ct = default)
    {
        IReadOnlyList<OrganisationImportRow> importRows;
        try
        {
            importRows = excelReader.Read(xlsxStream);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "Admin {ActorId} organisation import failed to parse the workbook", actorUserId);
            throw new ApiException(
                ErrorCodes.OrganisationImportFailed, 400,
                "The uploaded file could not be read as an Excel workbook.",
                "تعذّرت قراءة الملف المرفوع كمصنّف Excel.");
        }

        var inserted = 0;
        var updated = 0;
        var skipped = 0;
        var errors = new List<string>();
        var pending = 0;

        foreach (var row in importRows)
        {
            ct.ThrowIfCancellationRequested();

            var nameAr = NullIfBlank(row.NameAr);
            if (nameAr is null)
            {
                skipped++;
                AddError(errors, $"Row {row.RowNumber}: Arabic name is required.");
                continue;
            }

            var nameArClamped = Clamp(nameAr, 256)!;
            var cr = Clamp(NullIfBlank(row.CommercialRegistration), 32);
            var nameEn = Clamp(NullIfBlank(row.NameEn), 256);
            var sector = Clamp(NullIfBlank(row.Sector), 128);
            var city = Clamp(NullIfBlank(row.City), 128);
            var phone = Clamp(NullIfBlank(row.Phone), 32);
            var email = Clamp(NullIfBlank(row.Email), 320);
            var website = Clamp(NullIfBlank(row.Website), 512);

            var existing = cr is not null
                ? await db.Organisations
                    .SingleOrDefaultAsync(o => o.CommercialRegistration == cr, ct)
                : await db.Organisations
                    .SingleOrDefaultAsync(o => o.IsActive && o.NameArabic == nameArClamped, ct);

            var now = timeProvider.SimfNow();
            if (existing is null)
            {
                db.Organisations.Add(new Organisation
                {
                    Id = Guid.NewGuid(),
                    NameArabic = nameArClamped,
                    Name = nameEn,
                    CommercialRegistration = cr,
                    Sector = sector,
                    City = city,
                    Phone = phone,
                    Email = email,
                    Website = website,
                    IsActive = true,
                    CreatedAt = now,
                });
                inserted++;
            }
            else
            {
                existing.NameArabic = nameArClamped;
                existing.Name = nameEn;
                existing.CommercialRegistration = cr;
                existing.Sector = sector;
                existing.City = city;
                existing.Phone = phone;
                existing.Email = email;
                existing.Website = website;
                existing.UpdatedAt = now;
                updated++;
            }

            if (++pending >= ImportBatchSize)
            {
                await db.SaveChangesAsync(ct);
                pending = 0;
            }
        }

        if (pending > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.OrganisationImported,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"read={importRows.Count}; inserted={inserted}; updated={updated}; skipped={skipped}",
        }, ct);

        logger.LogInformation(
            "Admin {ActorId} imported organisations: read={Read} inserted={Inserted} updated={Updated} skipped={Skipped}",
            actorUserId, importRows.Count, inserted, updated, skipped);

        return new OrganisationImportResult(
            importRows.Count, inserted, updated, skipped, errors);
    }

    private sealed record OrganisationDraft(
        string NameAr, string? NameEn, string? CommercialRegistration,
        string? Sector, string? City, string? Phone, string? Email, string? Website);

    private static OrganisationDraft ValidateAndNormalise(
        string nameArRaw, string? nameEnRaw, string? commercialRegistrationRaw,
        string? sectorRaw, string? cityRaw, string? phoneRaw, string? emailRaw, string? websiteRaw)
    {
        var nameAr = (nameArRaw ?? string.Empty).Trim();
        if (nameAr.Length is < 1 or > 256)
        {
            throw new ApiException(
                ErrorCodes.OrganisationInvalid, 400,
                "Organisation Arabic name must be between 1 and 256 characters.",
                "يجب أن يتراوح طول الاسم العربي للمنظمة بين 1 و 256 حرفاً.");
        }

        // Optional fields — lengths mirror OrganisationConfiguration.HasMaxLength.
        var nameEn = OptionalText(
            nameEnRaw, 256, "Organisation English name", "الاسم الإنجليزي للمنظمة");
        var commercialRegistration = OptionalText(
            commercialRegistrationRaw, 32, "Commercial registration number", "رقم السجل التجاري");
        var sector = OptionalText(
            sectorRaw, 128, "Organisation sector", "قطاع المنظمة");
        var city = OptionalText(
            cityRaw, 128, "Organisation city", "مدينة المنظمة");
        var phone = OptionalText(
            phoneRaw, 32, "Organisation phone", "هاتف المنظمة");
        var email = OptionalText(
            emailRaw, 320, "Organisation email", "بريد المنظمة الإلكتروني");
        var website = OptionalText(
            websiteRaw, 512, "Organisation website", "الموقع الإلكتروني للمنظمة");

        return new OrganisationDraft(
            nameAr, nameEn, commercialRegistration, sector, city, phone, email, website);
    }

    private static string? OptionalText(string? raw, int maxLength, string fieldEn, string fieldAr)
    {
        var value = NullIfBlank(raw);
        if (value is not null && value.Length > maxLength)
        {
            throw new ApiException(
                ErrorCodes.OrganisationInvalid, 400,
                $"{fieldEn} must be {maxLength} characters or fewer.",
                $"يجب ألا يتجاوز {fieldAr} {maxLength} حرفاً.");
        }
        return value;
    }

    private static ApiException DuplicateCommercialRegistration(string commercialRegistration) =>
        new(
            ErrorCodes.OrganisationInvalid, 409,
            $"An organisation with commercial registration '{commercialRegistration}' already exists.",
            $"توجد منظمة بالسجل التجاري '{commercialRegistration}' بالفعل.");

    private static ApiException NotFound() =>
        new(
            ErrorCodes.OrganisationNotFound, 404,
            "The organisation was not found.",
            "لم يتم العثور على المنظمة.");

    private static void AddError(List<string> errors, string message)
    {
        if (errors.Count < ImportErrorCap)
        {
            errors.Add(message);
        }
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Clamp(string? value, int maxLength) =>
        value is not null && value.Length > maxLength ? value[..maxLength] : value;

    private static AdminOrganisationDetail ToDetail(Organisation o) => new(
        o.Id,
        o.NameArabic,
        o.Name,
        o.CommercialRegistration,
        o.Sector,
        o.City,
        o.Phone,
        o.Email,
        o.Website,
        o.IsActive,
        o.CreatedAt,
        o.UpdatedAt);
}
