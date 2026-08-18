// Tests: SIMF.Api.Tests/OrganisationTests.cs
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Organisations.Abstractions;
using SIMF.Common;
using SIMF.Common.Grids;
using SIMF.Contracts.Organisations;
using SIMF.Domain.Organisations;
using SIMF.Infrastructure.Common.Grids;
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

    /// <summary>
    /// The grid contract for /admin/organisations: one entry per key
    /// OrganisationsList can send. <c>name</c> is the ARABIC name and
    /// <c>nameEn</c> the English one, which is how the page labels its two name
    /// columns. <c>sector</c> is filterable but not searchable, matching the
    /// four-column search box the page describes.
    /// </summary>
    private static readonly GridColumns<Organisation> Columns = new GridColumns<Organisation>()
        .Add("name", org => org.NameArabic, searchable: true)
        .Add("nameEn", org => org.Name, searchable: true)
        .Add("commercialRegistration", org => org.CommercialRegistration, searchable: true)
        .Add("sector", org => org.Sector)
        .Add("city", org => org.City, searchable: true)
        .Add("isActive", org => org.IsActive)
        .DefaultOrder("name")
        .PageSize(fallback: 25, max: 200);

    private static readonly Expression<Func<Organisation, AdminOrganisationSummary>> ToSummary =
        org => new AdminOrganisationSummary(
            org.Id,
            org.NameArabic,
            org.Name,
            org.CommercialRegistration,
            org.Sector,
            org.City,
            org.IsActive);

    public Task<GridPage<AdminOrganisationSummary>> ListAsync(
        GridQuery query, CancellationToken ct = default) =>
        db.Organisations.ToGridPageAsync(query, Columns, org => org.Id, ToSummary, ct);

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
        var draft = ValidateAndNormalise(
            request.NameAr, request.NameEn, request.CommercialRegistration,
            request.Sector, request.City, request.Phone, request.Email, request.Website);

        if (draft.CommercialRegistration is not null)
        {
            var clash = await db.Organisations
                .AsNoTracking()
                .AnyAsync(row => row.CommercialRegistration == draft.CommercialRegistration, ct);
            if (clash)
            {
                throw DuplicateCommercialRegistration(draft.CommercialRegistration);
            }
        }

        var now = timeProvider.SimfNow();
        var org = new Organisation
        {
            Id = Guid.NewGuid(),
            NameArabic = draft.NameAr,
            Name = draft.NameEn,
            CommercialRegistration = draft.CommercialRegistration,
            Sector = draft.Sector,
            City = draft.City,
            Phone = draft.Phone,
            Email = draft.Email,
            Website = draft.Website,
            IsActive = true,
            CreatedAt = now,
        };
        db.Organisations.Add(org);
        await db.SaveChangesAsync(ct);

        await auditLog.WriteSuccessAsync(
            AuditEvents.OrganisationCreated,
            actorUserId,
            $"id={org.Id}; nameAr={draft.NameAr}; cr={draft.CommercialRegistration}",
            ct);

        logger.LogInformation(
            "Admin {ActorId} created Organisation {NameAr} ({Id})",
            actorUserId, draft.NameAr, org.Id);

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

        var draft = ValidateAndNormalise(
            request.NameAr, request.NameEn, request.CommercialRegistration,
            request.Sector, request.City, request.Phone, request.Email, request.Website);

        if (draft.CommercialRegistration is not null
            && !string.Equals(org.CommercialRegistration, draft.CommercialRegistration, StringComparison.OrdinalIgnoreCase))
        {
            var clash = await db.Organisations
                .AsNoTracking()
                .AnyAsync(row => row.Id != id && row.CommercialRegistration == draft.CommercialRegistration, ct);
            if (clash)
            {
                throw DuplicateCommercialRegistration(draft.CommercialRegistration);
            }
        }

        org.NameArabic = draft.NameAr;
        org.Name = draft.NameEn;
        org.CommercialRegistration = draft.CommercialRegistration;
        org.Sector = draft.Sector;
        org.City = draft.City;
        org.Phone = draft.Phone;
        org.Email = draft.Email;
        org.Website = draft.Website;
        org.IsActive = request.IsActive;
        org.UpdatedAt = timeProvider.SimfNow();
        await db.SaveChangesAsync(ct);

        await auditLog.WriteSuccessAsync(
            AuditEvents.OrganisationUpdated,
            actorUserId,
            $"id={org.Id}; nameAr={draft.NameAr}; active={org.IsActive}",
            ct);

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

        await auditLog.WriteSuccessAsync(
            AuditEvents.OrganisationDeactivated,
            actorUserId,
            $"id={org.Id}; nameAr={org.NameArabic}",
            ct);
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

        await auditLog.WriteSuccessAsync(
            AuditEvents.OrganisationImported,
            actorUserId,
            $"read={importRows.Count}; inserted={inserted}; updated={updated}; skipped={skipped}",
            ct);

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

    private static AdminOrganisationDetail ToDetail(Organisation organisation) => new(
        organisation.Id,
        organisation.NameArabic,
        organisation.Name,
        organisation.CommercialRegistration,
        organisation.Sector,
        organisation.City,
        organisation.Phone,
        organisation.Email,
        organisation.Website,
        organisation.IsActive,
        organisation.CreatedAt,
        organisation.UpdatedAt);
}
