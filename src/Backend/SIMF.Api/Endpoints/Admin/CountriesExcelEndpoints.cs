// Tests: SIMF.Api.Tests/CountriesExcelTests.cs
using System.Globalization;
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.Common.Abstractions;
using SIMF.Application.Excel;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/countries/export</c> — the D-356 grid export for the
/// Countries lookup. All the work lives in <see cref="AdminGridExportEndpoint{TRow}"/>;
/// this subclass only declares the route, permission, sheet/file names, the
/// column layout, and how to list a country row.
/// <para>Country's primary key is an <see cref="int"/> (ISO 3166-1 numeric),
/// not the <see cref="Guid"/> the generic export contract carries, so the CP
/// page always exports the current filtered set (it never sends selected Guid
/// ids) and <see cref="IdOf"/> is unused.</para>
/// </summary>
public sealed class ExportCountriesEndpoint(IAdminCountryService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<AdminCountrySummary>(exporter)
{
    protected override string RoutePath => "/admin/countries/export";
    protected override string Permission => PermissionCatalog.Countries.Export;
    protected override string SheetName => "Countries";
    protected override string FilePrefix => "simf-countries";

    protected override IReadOnlyList<GridExcelColumn<AdminCountrySummary>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<AdminCountrySummary>> _columns =
    [
        new("Id", row => row.Id),
        new("Code", row => row.Code),
        new("Name", row => row.Name),
        new("NameArabic", row => row.NameArabic),
        new("PhonePrefix", row => row.PhonePrefix),
        new("DisplayOrder", row => row.DisplayOrder),
        new("IsActive", row => row.IsActive),
        // D-506 — round-trip the delegation flag + arrival/departure dates
        // (appended so the existing column order is unchanged; import binds by
        // header name). The dates are blank unless the country is invited.
        new("IsInvited", row => row.IsInvited),
        new("DelegationArrivalDate", row => row.DelegationArrivalDate),
        new("DelegationDepartureDate", row => row.DelegationDepartureDate),
    ];

    protected override async Task<IReadOnlyList<AdminCountrySummary>> ListAsync(
        GridQuery query, CancellationToken ct) =>
        (await service.ListAllAsync(query, ct)).Items;

    // Country ids are int (ISO numeric), not the Guid the generic export
    // contract carries; the CP page always exports the filtered set, so IdOf
    // never participates in a selected-ids filter.
    protected override Guid IdOf(AdminCountrySummary row) => Guid.Empty;
}

/// <summary>
/// <c>POST /api/v1/admin/countries/import</c> — the D-356 grid import
/// (insert-only). The base does the upload defence, parse and per-row error
/// aggregation; this subclass binds one row to <see cref="AdminCreateCountryRequest"/>
/// and creates it (the service rejects a duplicate id/code and any invalid field
/// with an <c>ApiException</c>, which the base records as a per-row error rather
/// than aborting the batch).
/// <para>D-506 — the delegation flag + arrival/departure dates round-trip through
/// import (<c>IsInvited</c> accepts Yes/No or true/false; the dates ISO yyyy-MM-dd;
/// the service drops the dates when the row is not invited).</para>
/// <para><b>Omitted column:</b> <c>HeadOfDelegationUserProfileId</c> (the head of
/// delegation, D-499) is a UserProfile directory FK chosen with the CP picker — a
/// raw GUID is not sane flat-Excel content — so import always leaves it unset; an
/// admin links the head afterwards via Edit (same reason ContactId is omitted on
/// the Sponsor/Exhibitor imports).</para>
/// </summary>
public sealed class ImportCountriesEndpoint(IAdminCountryService service, IGridExcelImporter importer)
    : AdminGridImportEndpoint(importer)
{
    protected override string RoutePath => "/admin/countries/import";
    protected override string Permission => PermissionCatalog.Countries.Import;
    protected override string SheetName => "Countries";
    protected override IReadOnlyList<string> RequiredHeaders => ["Id", "Code", "Name", "NameArabic"];

    protected override string? RowKey(GridImportRow row) =>
        row.Cells.TryGetValue("Code", out var code) ? code : null;

    protected override async Task<GridRowApplyKind> ApplyRowAsync(
        Guid actorId, GridImportRow row, CancellationToken ct)
    {
        var idText = row.Cells.GetValueOrDefault("Id", string.Empty);
        if (!int.TryParse(idText, out var id) || id <= 0)
        {
            throw new DataValidationException(
                "The country id must be a positive integer (ISO 3166-1 numeric).",
                "يجب أن يكون معرّف البلد عدداً صحيحاً موجباً (ISO 3166-1).");
        }

        var code = row.Cells.GetValueOrDefault("Code", string.Empty);
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DataValidationException(
                "The country code is required.",
                "رمز البلد مطلوب.");
        }

        var name = row.Cells.GetValueOrDefault("Name", string.Empty);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DataValidationException(
                "The English name is required.",
                "الاسم بالإنجليزية مطلوب.");
        }

        var nameArabic = row.Cells.GetValueOrDefault("NameArabic", string.Empty);
        if (string.IsNullOrWhiteSpace(nameArabic))
        {
            throw new DataValidationException(
                "The Arabic name is required.",
                "الاسم بالعربية مطلوب.");
        }

        var phonePrefix = row.Cells.GetValueOrDefault("PhonePrefix", string.Empty);
        await service.CreateAsync(actorId, new AdminCreateCountryRequest
        {
            Id = id,
            Code = code,
            Name = name,
            NameArabic = nameArabic,
            PhonePrefix = string.IsNullOrWhiteSpace(phonePrefix) ? null : phonePrefix,
            DisplayOrder = int.TryParse(
                row.Cells.GetValueOrDefault("DisplayOrder", string.Empty), out var order) ? order : 0,
            // D-506 — round-trip the delegation flag + arrival/departure dates.
            // The service clears the dates when IsInvited is false, so a date on a
            // non-invited row is simply dropped (matching the CP Edit form).
            IsInvited = ParseInvited(row.Cells.GetValueOrDefault("IsInvited", string.Empty)),
            DelegationArrivalDate = ParseDate(
                row.Cells.GetValueOrDefault("DelegationArrivalDate", string.Empty),
                "DelegationArrivalDate"),
            DelegationDepartureDate = ParseDate(
                row.Cells.GetValueOrDefault("DelegationDepartureDate", string.Empty),
                "DelegationDepartureDate"),
        }, ct);
        return GridRowApplyKind.Created;
    }

    // Maps an IsInvited cell to a bool. Accepts the export's "Yes"/"No" as well
    // as "true"/"false"/"1"/"0"; blank → false (a not-invited country). Any other
    // non-blank value is a per-row error rather than a silent default.
    private static bool ParseInvited(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }
        return trimmed.ToLowerInvariant() switch
        {
            "yes" or "true" or "1" => true,
            "no" or "false" or "0" => false,
            _ => throw new DataValidationException(
                "IsInvited must be Yes or No.",
                "يجب أن تكون قيمة \"مدعوة\" نعم أو لا."),
        };
    }

    // Parses a delegation date cell. Blank → null (optional). A non-blank value
    // must be a valid date (ISO yyyy-MM-dd preferred); an unparseable value is a
    // per-row error naming the column rather than a silent drop.
    private static DateOnly? ParseDate(string value, string columnName)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }
        if (DateOnly.TryParse(trimmed, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
        {
            return date;
        }
        throw new DataValidationException(
            $"{columnName} must be a valid date (yyyy-MM-dd).",
            $"يجب أن يكون '{columnName}' تاريخاً صالحاً (yyyy-MM-dd).");
    }
}
