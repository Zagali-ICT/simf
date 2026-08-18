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
/// exact active Arabic name. On the update side the import only ever fills
/// columns the sheet supplied — a blank cell is "not supplied", not "clear it",
/// so a partial sheet cannot wipe curated data.
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

    /// <summary>The most match keys sent in one <c>IN (...)</c> pre-load query.</summary>
    private const int ImportKeyChunkSize = 500;

    // Column lengths, mirrored from OrganisationConfiguration. These are the
    // stored widths, so validation and the import clamp must both use them: a
    // validator that admits more than the column holds turns a legitimate row
    // into an unhandled SqlException on SaveChanges, and a clamp that cuts
    // shorter than the column silently truncates the sheet.
    private const int NameMaxLength = 150;
    private const int CommercialRegistrationMaxLength = 700;
    private const int SectorMaxLength = 128;
    private const int CityMaxLength = 128;
    private const int PhoneMaxLength = 32;
    private const int EmailMaxLength = 320;
    private const int WebsiteMaxLength = 512;

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

        var skipped = 0;
        var errors = new List<string>();
        var rows = new List<ImportDraft>(importRows.Count);

        foreach (var row in importRows)
        {
            var nameAr = NullIfBlank(row.NameAr);
            if (nameAr is null)
            {
                skipped++;
                AddError(errors, $"Row {row.RowNumber}: Arabic name is required.");
                continue;
            }

            rows.Add(new ImportDraft(
                Clamp(nameAr, NameMaxLength)!,
                Clamp(NullIfBlank(row.CommercialRegistration), CommercialRegistrationMaxLength),
                Clamp(NullIfBlank(row.NameEn), NameMaxLength),
                Clamp(NullIfBlank(row.Sector), SectorMaxLength),
                Clamp(NullIfBlank(row.City), CityMaxLength),
                Clamp(NullIfBlank(row.Phone), PhoneMaxLength),
                Clamp(NullIfBlank(row.Email), EmailMaxLength),
                Clamp(NullIfBlank(row.Website), WebsiteMaxLength)));
        }

        var byCommercialRegistration = await LoadByCommercialRegistrationAsync(rows, ct);
        var byArabicName = await LoadByActiveArabicNameAsync(rows, ct);

        var inserted = 0;
        var updated = 0;
        var pending = 0;

        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();

            var matched = row.CommercialRegistration is not null
                ? byCommercialRegistration.GetValueOrDefault(row.CommercialRegistration)
                : byArabicName.GetValueOrDefault(row.NameArabic);

            var now = timeProvider.SimfNow();
            if (matched is null)
            {
                var created = NewOrganisation(row, now);
                db.Organisations.Add(created);

                // Register the new row so a later sheet row carrying the same
                // key updates it instead of inserting a second copy — an
                // unsaved entity is invisible to a query, so two rows sharing a
                // commercial registration used to reach the unique index and
                // fail the whole import.
                if (created.CommercialRegistration is not null)
                {
                    byCommercialRegistration.TryAdd(created.CommercialRegistration, created);
                }
                byArabicName.TryAdd(created.NameArabic, created);
                inserted++;
            }
            else
            {
                ApplyImportUpdate(matched, row, now);
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

    /// <summary>One spreadsheet row, trimmed and clamped to the stored column
    /// widths. A <c>null</c> optional value means the sheet did not supply the
    /// cell, which the update path treats as "leave it alone".</summary>
    private sealed record ImportDraft(
        string NameArabic, string? CommercialRegistration, string? NameEn,
        string? Sector, string? City, string? Phone, string? Email, string? Website);

    /// <summary>
    /// Pre-loads every organisation the sheet could match on commercial
    /// registration, keyed case-insensitively like the database compares it.
    /// Tracked on purpose — the update path mutates what this returns — and
    /// chunked so a large sheet cannot overrun the parameter limit of a single
    /// <c>IN (...)</c>.
    /// </summary>
    private async Task<Dictionary<string, Organisation>> LoadByCommercialRegistrationAsync(
        List<ImportDraft> rows, CancellationToken ct)
    {
        var keys = rows
            .Where(row => row.CommercialRegistration is not null)
            .Select(row => row.CommercialRegistration!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var matched = NewKeyMap();
        foreach (var chunk in keys.Chunk(ImportKeyChunkSize))
        {
            var found = await db.Organisations
                .Where(org => org.CommercialRegistration != null
                    && chunk.Contains(org.CommercialRegistration!))
                .OrderBy(org => org.CreatedAt)
                .ThenBy(org => org.Id)
                .ToListAsync(ct);
            foreach (var org in found)
            {
                matched.TryAdd(org.CommercialRegistration!, org);
            }
        }

        return matched;
    }

    /// <summary>
    /// Pre-loads the active organisations the sheet could match by Arabic name.
    /// The name is NOT unique, so the oldest row wins deterministically rather
    /// than the lookup throwing — two same-named organisations must not break
    /// every later import.
    /// </summary>
    private async Task<Dictionary<string, Organisation>> LoadByActiveArabicNameAsync(
        List<ImportDraft> rows, CancellationToken ct)
    {
        var keys = rows
            .Where(row => row.CommercialRegistration is null)
            .Select(row => row.NameArabic)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var matched = NewKeyMap();
        foreach (var chunk in keys.Chunk(ImportKeyChunkSize))
        {
            var found = await db.Organisations
                .Where(org => org.IsActive && chunk.Contains(org.NameArabic))
                .OrderBy(org => org.CreatedAt)
                .ThenBy(org => org.Id)
                .ToListAsync(ct);
            foreach (var org in found)
            {
                matched.TryAdd(org.NameArabic, org);
            }
        }

        return matched;
    }

    /// <summary>Match keys compare case-insensitively, the way the database
    /// collation compares them.</summary>
    private static Dictionary<string, Organisation> NewKeyMap() =>
        new(StringComparer.OrdinalIgnoreCase);

    private static Organisation NewOrganisation(ImportDraft row, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        NameArabic = row.NameArabic,
        Name = row.NameEn,
        CommercialRegistration = row.CommercialRegistration,
        Sector = row.Sector,
        City = row.City,
        Phone = row.Phone,
        Email = row.Email,
        Website = row.Website,
        IsActive = true,
        CreatedAt = now,
    };

    /// <summary>
    /// Applies a sheet row over an existing organisation. Every optional column
    /// coalesces: a blank cell in a bulk sheet means "not supplied", never
    /// "clear it", so a partial-update sheet carrying only the Arabic name
    /// cannot erase curated columns. Clearing a field deliberately is what the
    /// explicit admin edit form is for.
    /// </summary>
    private static void ApplyImportUpdate(Organisation existing, ImportDraft row, DateTime now)
    {
        existing.NameArabic = row.NameArabic;
        existing.CommercialRegistration = row.CommercialRegistration ?? existing.CommercialRegistration;
        existing.Name = row.NameEn ?? existing.Name;
        existing.Sector = row.Sector ?? existing.Sector;
        existing.City = row.City ?? existing.City;
        existing.Phone = row.Phone ?? existing.Phone;
        existing.Email = row.Email ?? existing.Email;
        existing.Website = row.Website ?? existing.Website;
        existing.UpdatedAt = now;
    }

    private sealed record OrganisationDraft(
        string NameAr, string? NameEn, string? CommercialRegistration,
        string? Sector, string? City, string? Phone, string? Email, string? Website);

    private static OrganisationDraft ValidateAndNormalise(
        string nameArRaw, string? nameEnRaw, string? commercialRegistrationRaw,
        string? sectorRaw, string? cityRaw, string? phoneRaw, string? emailRaw, string? websiteRaw)
    {
        var nameAr = (nameArRaw ?? string.Empty).Trim();
        if (nameAr.Length is < 1 or > NameMaxLength)
        {
            throw new ApiException(
                ErrorCodes.OrganisationInvalid, 400,
                $"Organisation Arabic name must be between 1 and {NameMaxLength} characters.",
                $"يجب أن يتراوح طول الاسم العربي للمنظمة بين 1 و {NameMaxLength} حرفاً.");
        }

        // Optional fields — lengths mirror OrganisationConfiguration.HasMaxLength.
        var nameEn = OptionalText(
            nameEnRaw, NameMaxLength, "Organisation English name", "الاسم الإنجليزي للمنظمة");
        var commercialRegistration = OptionalText(
            commercialRegistrationRaw, CommercialRegistrationMaxLength,
            "Commercial registration number", "رقم السجل التجاري");
        var sector = OptionalText(
            sectorRaw, SectorMaxLength, "Organisation sector", "قطاع المنظمة");
        var city = OptionalText(
            cityRaw, CityMaxLength, "Organisation city", "مدينة المنظمة");
        var phone = OptionalText(
            phoneRaw, PhoneMaxLength, "Organisation phone", "هاتف المنظمة");
        var email = OptionalText(
            emailRaw, EmailMaxLength, "Organisation email", "بريد المنظمة الإلكتروني");
        var website = OptionalText(
            websiteRaw, WebsiteMaxLength, "Organisation website", "الموقع الإلكتروني للمنظمة");

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
