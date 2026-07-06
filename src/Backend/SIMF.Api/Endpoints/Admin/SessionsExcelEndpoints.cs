// Tests: SIMF.Api.Tests/SessionsExcelTests.cs
using System.Globalization;
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.Excel;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/sessions/export</c> — the D-356 grid export for the
/// programme Sessions (D-165, PDF §2.9). All the work lives in
/// <see cref="AdminGridExportEndpoint{TRow}"/>; this subclass declares the route,
/// permission, sheet/file names, the column layout (mirroring the Sessions grid)
/// and how to list + identify a session row (the same
/// <see cref="IAdminSessionService.ListAllAsync"/> the list endpoint calls).
/// <para>The two foreign keys are exported by a human-readable natural key so the
/// workbook round-trips back through import: the Hall as its code and the optional
/// Category as its English name. The <c>StartUtc</c> / <c>EndUtc</c> window writes
/// a round-trip-safe ISO-8601 UTC string, the lifecycle <c>Status</c> writes its
/// enum name. The hall/category maps are built once per request inside
/// <see cref="ListAsync"/> (the base reads <see cref="Columns"/> straight after),
/// so the column selectors resolve a name without an extra round-trip per row.
/// </para>
/// <para><b>Omitted columns:</b> the session's speaker / host roster and its
/// theme set are many-to-many collections that cannot be expressed safely as a
/// single text cell, so the export leaves them out (and import never sets them);
/// an admin manages them afterwards via Edit.</para>
/// </summary>
public sealed class ExportSessionsEndpoint(
    IAdminSessionService service,
    IAdminHallService hallService,
    IAdminSessionCategoryService categoryService,
    IGridExcelExporter exporter)
    : AdminGridExportEndpoint<AdminSessionSummary>(exporter)
{
    protected override string RoutePath => "/admin/sessions/export";
    protected override string Permission => PermissionCatalog.Sessions.Export;
    protected override string SheetName => "Sessions";
    protected override string FilePrefix => "simf-sessions";

    // Built once per request in ListAsync; the base reads Columns right afterwards.
    private Dictionary<Guid, string> _hallCodes = new();
    private Dictionary<Guid, string> _categoryNames = new();
    private bool _lookupsLoaded;

    protected override IReadOnlyList<GridExcelColumn<AdminSessionSummary>> Columns =>
    [
        new("Code", row => row.Code),
        new("Title", row => row.Title),
        new("TitleArabic", row => row.TitleArabic),
        new("Hall", row => _hallCodes.TryGetValue(row.HallId, out var code) ? code : string.Empty),
        new("Category", row => row.CategoryId is { } id
            && _categoryNames.TryGetValue(id, out var name) ? name : string.Empty),
        new("StartUtc", row => row.StartUtc.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture)),
        new("EndUtc", row => row.EndUtc.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture)),
        new("Capacity", row => row.Capacity),
        new("Status", row => row.Status.ToString()),
        new("IsActive", row => row.IsActive),
        // D-506 — round-trip the eight fields the IO boundary dropped (appended
        // so the existing column order is unchanged; import binds by header name).
        // The two enums write their display name (blank when unset); import reads
        // either the name or the raw int back.
        new("Type", row => row.Type?.ToString()),
        new("SeatSelectionModeOverride", row => row.SeatSelectionModeOverride?.ToString()),
        new("Description", row => row.Description),
        new("DescriptionArabic", row => row.DescriptionArabic),
        new("LiveStreamUrl", row => row.LiveStreamUrl),
        new("LiveSignLanguageUrl", row => row.LiveSignLanguageUrl),
        new("LiveCaptions", row => row.LiveCaptions),
        new("LiveCaptionsArabic", row => row.LiveCaptionsArabic),
    ];

    protected override async Task<IReadOnlyList<AdminSessionSummary>> ListAsync(
        GridQuery query, CancellationToken ct)
    {
        var rows = (await service.ListAllAsync(query, ct)).Items;

        // Resolve the two FK display values once per request. FastEndpoints creates
        // one endpoint instance per request and the export base calls this once per
        // page, so the guard keeps the bounded lookups to a single read on the first
        // page (Top=500 — the same page size the CP session form uses for these
        // pickers).
        if (!_lookupsLoaded)
        {
            var lookupQuery = new GridQuery { Skip = 0, Top = 500 };
            var halls = (await hallService.ListAllAsync(lookupQuery, ct)).Items;
            var categories = (await categoryService.ListAsync(lookupQuery, ct)).Items;
            _hallCodes = halls.ToDictionary(h => h.Id, h => h.Code);
            _categoryNames = categories.ToDictionary(c => c.Id, c => c.Name);
            _lookupsLoaded = true;
        }

        return rows;
    }

    protected override Guid IdOf(AdminSessionSummary row) => row.Id;
}

/// <summary>
/// <c>POST /api/v1/admin/sessions/import</c> — the D-356 grid import (insert-only)
/// for the programme Sessions. The base does the upload defence, parse and per-row
/// error aggregation; this subclass binds one row to
/// <see cref="AdminCreateSessionRequest"/> and creates it (the service rejects a
/// duplicate code or an invalid time window → a per-row error, not a batch abort).
/// <para>
/// The mandatory <b>Hall</b> foreign key is resolved from its code (the value the
/// export writes); a blank or unresolved Hall is a per-row
/// <see cref="DataValidationException"/> (the create cannot proceed without it).
/// The optional <b>Category</b> is resolved from its English name — blank leaves
/// it unset; a non-blank value that does not resolve to an active category is a
/// per-row error. Both lookups are case-insensitive and active-only.
/// </para>
/// <para><b>Omitted columns:</b> the session's speaker / host roster and its theme
/// set are many-to-many collections that cannot be expressed safely as plain text
/// in a bulk import, so import always leaves them empty; an admin sets them
/// afterwards via Edit.</para>
/// </summary>
public sealed class ImportSessionsEndpoint(
    IAdminSessionService service,
    IAdminHallService hallService,
    IAdminSessionCategoryService categoryService,
    IGridExcelImporter importer)
    : AdminGridImportEndpoint(importer)
{
    protected override string RoutePath => "/admin/sessions/import";
    protected override string Permission => PermissionCatalog.Sessions.Import;
    protected override string SheetName => "Sessions";
    protected override IReadOnlyList<string> RequiredHeaders =>
        ["Code", "Title", "TitleArabic", "Hall", "StartUtc", "EndUtc"];

    protected override string? RowKey(GridImportRow row) =>
        row.Cells.TryGetValue("Code", out var code) ? code : null;

    protected override async Task<GridRowApplyKind> ApplyRowAsync(
        Guid actorId, GridImportRow row, CancellationToken ct)
    {
        var code = row.Cells.GetValueOrDefault("Code", string.Empty);
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length is < 2 or > 16)
        {
            throw new DataValidationException(
                "Code must be between 2 and 16 characters.",
                "يجب أن يكون الرمز بين حرفين و16 حرفًا.");
        }

        var title = row.Cells.GetValueOrDefault("Title", string.Empty);
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 256)
        {
            throw new DataValidationException(
                "The English title is required (max 256 characters).",
                "العنوان بالإنجليزية مطلوب (حتى 256 حرفًا).");
        }

        var titleArabic = row.Cells.GetValueOrDefault("TitleArabic", string.Empty);
        if (string.IsNullOrWhiteSpace(titleArabic) || titleArabic.Trim().Length > 256)
        {
            throw new DataValidationException(
                "The Arabic title is required (max 256 characters).",
                "العنوان بالعربية مطلوب (حتى 256 حرفًا).");
        }

        var hallId = await ResolveHallAsync(
            row.Cells.GetValueOrDefault("Hall", string.Empty), ct);
        var categoryId = await ResolveCategoryAsync(
            row.Cells.GetValueOrDefault("Category", string.Empty), ct);

        var start = ParseUtc(row.Cells.GetValueOrDefault("StartUtc", string.Empty), "StartUtc");
        var end = ParseUtc(row.Cells.GetValueOrDefault("EndUtc", string.Empty), "EndUtc");
        if (end <= start)
        {
            throw new DataValidationException(
                "The end time must be after the start time.",
                "يجب أن يكون وقت الانتهاء بعد وقت البدء.");
        }

        int? capacityOverride = null;
        var capacityRaw = row.Cells.GetValueOrDefault("Capacity", string.Empty);
        if (!string.IsNullOrWhiteSpace(capacityRaw))
        {
            if (!int.TryParse(capacityRaw.Trim(), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
            {
                throw new DataValidationException(
                    "Capacity must be a non-negative whole number.",
                    "يجب أن تكون السعة رقمًا صحيحًا غير سالب.");
            }
            capacityOverride = parsed;
        }

        await service.CreateAsync(actorId, new AdminCreateSessionRequest
        {
            Code = code.Trim().ToUpperInvariant(),
            Title = title.Trim(),
            TitleArabic = titleArabic.Trim(),
            HallId = hallId,
            CategoryId = categoryId,
            StartUtc = start,
            EndUtc = end,
            CapacityOverride = capacityOverride,
            // D-506 — round-trip the eight fields the import previously dropped
            // (the service trims and length-guards the strings; absent columns
            // simply stay null). The two enums accept the display name or the raw
            // int; blank → null; an unknown non-blank value is a per-row error.
            Description = NullIfBlank(row.Cells.GetValueOrDefault("Description", string.Empty)),
            DescriptionArabic = NullIfBlank(row.Cells.GetValueOrDefault("DescriptionArabic", string.Empty)),
            LiveStreamUrl = NullIfBlank(row.Cells.GetValueOrDefault("LiveStreamUrl", string.Empty)),
            LiveSignLanguageUrl = NullIfBlank(row.Cells.GetValueOrDefault("LiveSignLanguageUrl", string.Empty)),
            LiveCaptions = NullIfBlank(row.Cells.GetValueOrDefault("LiveCaptions", string.Empty)),
            LiveCaptionsArabic = NullIfBlank(row.Cells.GetValueOrDefault("LiveCaptionsArabic", string.Empty)),
            Type = ParseType(row.Cells.GetValueOrDefault("Type", string.Empty)),
            SeatSelectionModeOverride = ParseSeatSelectionMode(
                row.Cells.GetValueOrDefault("SeatSelectionModeOverride", string.Empty)),
        }, ct);
        return GridRowApplyKind.Created;
    }

    // Maps a Type cell to its enum value, or null when blank (the type is
    // optional). Accepts the display name (Workshop/Session/Event, as the export
    // writes) or the raw int — keep aligned with SIMF.Common.Enums.SessionType.
    private static SessionType? ParseType(string value)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }
        if (int.TryParse(trimmed, out var raw) && Enum.IsDefined(typeof(SessionType), raw))
        {
            return (SessionType)raw;
        }
        return trimmed.ToLowerInvariant() switch
        {
            "workshop" => SessionType.Workshop,
            "session" => SessionType.Session,
            "event" => SessionType.Event,
            _ => throw new DataValidationException(
                "The type must be one of Workshop, Session or Event.",
                "يجب أن يكون النوع إحدى: ورشة عمل أو جلسة أو حدث."),
        };
    }

    // Maps a SeatSelectionModeOverride cell to its enum value, or null when blank
    // (the override is optional — null inherits the hall). Accepts the display
    // name (AssignedSeat/OpenSeating, as the export writes) or the raw int — keep
    // aligned with SIMF.Common.Enums.SeatSelectionMode.
    private static SeatSelectionMode? ParseSeatSelectionMode(string value)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }
        if (int.TryParse(trimmed, out var raw) && Enum.IsDefined(typeof(SeatSelectionMode), raw))
        {
            return (SeatSelectionMode)raw;
        }
        return trimmed.ToLowerInvariant() switch
        {
            "assignedseat" => SeatSelectionMode.AssignedSeat,
            "openseating" => SeatSelectionMode.OpenSeating,
            _ => throw new DataValidationException(
                "The seat-selection mode must be one of AssignedSeat or OpenSeating.",
                "يجب أن يكون نمط اختيار المقعد إحدى: مقعد محدد أو جلوس مفتوح."),
        };
    }

    // Resolves the mandatory Hall by its code (the value the export writes,
    // case-insensitive, active only). A blank or unresolved value is a per-row
    // error rather than a silent skip — the session cannot be created without a
    // hall.
    private async Task<Guid> ResolveHallAsync(string value, CancellationToken ct)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            throw new DataValidationException(
                "The hall code is required.",
                "رمز القاعة مطلوب.");
        }

        var halls = (await hallService
            .ListAllAsync(new GridQuery { Top = 500, Search = trimmed }, ct)).Items;
        var match = halls.FirstOrDefault(h => h.IsActive
            && string.Equals(h.Code, trimmed, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            throw new DataValidationException(
                $"No active hall with code '{trimmed}' was found.",
                $"لم يتم العثور على قاعة مفعّلة بالرمز '{trimmed}'.");
        }
        return match.Id;
    }

    // Resolves an optional Category by its English name (the value the export
    // writes). Blank → unset. A non-blank value that does not match an active
    // category is a per-row error.
    private async Task<Guid?> ResolveCategoryAsync(string value, CancellationToken ct)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0) { return null; }

        var categories = (await categoryService
            .ListAsync(new GridQuery { Top = 500, Search = trimmed }, ct)).Items;
        var match = categories.FirstOrDefault(c => c.IsActive
            && string.Equals(c.Name, trimmed, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            throw new DataValidationException(
                $"No active session category named '{trimmed}' was found.",
                $"لم يتم العثور على تصنيف جلسة مفعّل باسم '{trimmed}'.");
        }
        return match.Id;
    }

    // Parses a UTC instant from the cell (the export writes ISO-8601 with a 'Z').
    // Any non-blank value that the round-trip / general parser cannot read is a
    // per-row error.
    private static DateTimeOffset ParseUtc(string value, string field)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0
            || !DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            throw new DataValidationException(
                $"{field} must be a valid date/time (e.g. 2026-01-30T09:00:00Z).",
                $"يجب أن يكون {field} تاريخًا/وقتًا صالحًا (مثال: 2026-01-30T09:00:00Z).");
        }
        return parsed.ToUniversalTime();
    }

    private static string? NullIfBlank(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
