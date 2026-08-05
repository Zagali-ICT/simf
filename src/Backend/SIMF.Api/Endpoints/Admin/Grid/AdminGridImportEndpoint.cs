using FastEndpoints;
using SIMF.Api.RequestContext;
using SIMF.Application.Excel;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin.Grid;

/// <summary>How a single imported row was applied.</summary>
public enum GridRowApplyKind
{
    Created,
    Updated,
    Skipped,
}

/// <summary>
/// Generic base for a resource's <c>POST /admin/{resource}/import</c> endpoint
/// (D-356). A concrete subclass supplies the route, the <c>{Resource}.Import</c>
/// permission, the sheet name + required headers, a per-row key (for the error
/// list) and how to apply one parsed row to its create/upsert service; this
/// base owns the auth gate, the 5 MB + ZIP-magic upload defence (D-045 H1), the
/// parse, the per-row try/catch (one bad row never aborts the batch) and the
/// aggregated <see cref="AdminGridImportResult"/>. Mirrors the proven
/// <c>ImportUsersEndpoint</c>.
/// </summary>
public abstract class AdminGridImportEndpoint(IGridExcelImporter importer)
    : Endpoint<EmptyRequest, ApiResult<AdminGridImportResult>>
{
    /// <summary>Maximum accepted upload size in bytes (5 MB).</summary>
    protected const long MaxUploadBytes = 5L * 1024 * 1024;

    /// <summary>The maximum data rows an import workbook may carry.</summary>
    protected const int MaxImportRows = 5_000;

    /// <summary>ZIP local-file-header magic — every .xlsx workbook starts with these four bytes.</summary>
    private static readonly byte[] ZipMagic = { 0x50, 0x4B, 0x03, 0x04 };

    /// <summary>The endpoint route, e.g. <c>/admin/interests/import</c>.</summary>
    protected abstract string RoutePath { get; }

    /// <summary>The <c>{Resource}.Import</c> permission code that gates this endpoint.</summary>
    protected abstract string Permission { get; }

    /// <summary>The worksheet name the workbook must contain (exact match).</summary>
    protected abstract string SheetName { get; }

    /// <summary>The header columns that must be present (case-insensitive).</summary>
    protected abstract IReadOnlyList<string> RequiredHeaders { get; }

    /// <summary>A human-recognisable value for the row (e.g. its name/code),
    /// echoed in the per-row error list so the admin can find the offending row.</summary>
    protected abstract string? RowKey(GridImportRow row);

    /// <summary>
    /// Applies one parsed row to the resource's create/upsert service. Return
    /// the outcome; throw <see cref="DataValidationException"/> or
    /// <see cref="ApiException"/> for an invalid row — the base catches it and
    /// records a per-row error without aborting the rest of the batch.
    /// </summary>
    protected abstract Task<GridRowApplyKind> ApplyRowAsync(
        Guid actorId, GridImportRow row, CancellationToken ct);

    public override void Configure()
    {
        Post(RoutePath);
        Policies(PermissionCatalog.PolicyFor(Permission), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        AllowFileUploads();
        Summary(summary => summary.Summary =
            "Import grid rows from an XLSX workbook. Returns the per-row outcome.");
    }

    public override async Task HandleAsync(EmptyRequest _, CancellationToken ct)
    {
        var actorId = User.ActorId();

        var file = Files.GetFile("file");
        if (file is null || file.Length == 0)
        {
            throw new DataValidationException(
                "An Excel file is required.",
                "ملف Excel مطلوب.");
        }
        if (file.Length > MaxUploadBytes)
        {
            throw new ApiException(
                ErrorCodes.AdminImportEmpty, 413,
                "The Excel file is too large. The maximum is 5 MB.",
                "ملف Excel كبير جدًا. الحد الأقصى 5 ميغابايت.");
        }

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        var bytes = stream.ToArray();
        if (!HasZipMagic(bytes))
        {
            throw new DataValidationException(
                "The file is not a valid Excel workbook.",
                "الملف ليس مصنف Excel صالحًا.");
        }

        var sheet = importer.Parse(bytes, SheetName, RequiredHeaders, MaxImportRows);

        var created = 0;
        var updated = 0;
        var skipped = 0;
        var errors = new List<GridImportRowError>();
        foreach (var row in sheet.Rows)
        {
            try
            {
                var outcome = await ApplyRowAsync(actorId, row, ct);
                switch (outcome)
                {
                    case GridRowApplyKind.Created: created++; break;
                    case GridRowApplyKind.Updated: updated++; break;
                    default: skipped++; break;
                }
            }
            catch (DataValidationException exception)
            {
                errors.Add(new GridImportRowError(row.RowNumber, RowKey(row), exception.Message));
            }
            catch (ApiException exception)
            {
                errors.Add(new GridImportRowError(row.RowNumber, RowKey(row), exception.Message));
            }
        }

        var result = new AdminGridImportResult(created, updated, skipped, errors);
        await Send.OkAsync(ApiResult<AdminGridImportResult>.Ok(result), ct);
    }

    private static bool HasZipMagic(byte[] bytes)
    {
        if (bytes.Length < ZipMagic.Length) return false;
        for (var i = 0; i < ZipMagic.Length; i++)
        {
            if (bytes[i] != ZipMagic[i]) return false;
        }
        return true;
    }
}
