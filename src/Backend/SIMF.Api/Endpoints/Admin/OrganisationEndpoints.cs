// Tests: SIMF.Api.Tests/OrganisationTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.Organisations.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Organisations;

namespace SIMF.Api.Endpoints.Admin;

// Organisation lookup module — admin CRUD + gov-Excel import + the public
// picker search, all in one file. Validation lives in the service (mirrors
// AdminBoothService); endpoints just gate + bind + delegate.
//
// CreateOrganisationRequest / UpdateOrganisationRequest carry no route id, so
// the {id} PUT reads the route GUID via Route<Guid>("id") (FAQ pattern) rather
// than a derived route-merge type. GET/DELETE bind the {id} route to this small
// DTO.
public sealed class OrganisationByIdRequest
{
    /// <summary>The organisation's primary key, bound from the <c>{id}</c> route segment.</summary>
    public Guid Id { get; set; }
}

/// <summary><c>POST /admin/organisations/list</c> — paged admin grid over
/// <c>Organisations</c>. Mirrors ListBoothsEndpoint.</summary>
public sealed class ListOrganisationsEndpoint(IAdminOrganisationService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminOrganisationSummary>>>
{
    public override void Configure()
    {
        Post("/admin/organisations/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Organisations.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminOrganisationSummary>>.Ok(
            await service.ListAsync(req, ct)), ct);
}

/// <summary><c>GET /admin/organisations/{id}</c> — a single organisation's full
/// detail. Mirrors GetBoothEndpoint.</summary>
public sealed class GetOrganisationEndpoint(IAdminOrganisationService service)
    : Endpoint<OrganisationByIdRequest, ApiResult<AdminOrganisationDetail>>
{
    public override void Configure()
    {
        Get("/admin/organisations/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Organisations.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(OrganisationByIdRequest req, CancellationToken ct)
    {
        var detail = await service.GetAsync(req.Id, ct)
            ?? throw new ApiException(
                ErrorCodes.OrganisationNotFound, 404,
                "The organisation was not found.",
                "لم يتم العثور على المنشأة.");
        await Send.OkAsync(ApiResult<AdminOrganisationDetail>.Ok(detail), ct);
    }
}

/// <summary><c>POST /admin/organisations</c> — create a new organisation.
/// Mirrors CreateBoothEndpoint.</summary>
public sealed class CreateOrganisationEndpoint(IAdminOrganisationService service)
    : Endpoint<CreateOrganisationRequest, ApiResult<AdminOrganisationDetail>>
{
    public override void Configure()
    {
        Post("/admin/organisations");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Organisations.Create),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(CreateOrganisationRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminOrganisationDetail>.Ok(
            await service.CreateAsync(actorId, req, ct)), ct);
    }
}

/// <summary><c>PUT /admin/organisations/{id}</c> — update an organisation.
/// Reads the route GUID via <c>Route&lt;Guid&gt;("id")</c> (FAQ pattern); the
/// contract request carries no id. Mirrors UpdateFaqGroupEndpoint.</summary>
public sealed class UpdateOrganisationEndpoint(IAdminOrganisationService service)
    : Endpoint<UpdateOrganisationRequest, ApiResult<AdminOrganisationDetail>>
{
    public override void Configure()
    {
        Put("/admin/organisations/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Organisations.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(UpdateOrganisationRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminOrganisationDetail>.Ok(
            await service.UpdateAsync(actorId, Route<Guid>("id"), req, ct)), ct);
    }
}

/// <summary><c>DELETE /admin/organisations/{id}</c> — soft-deactivate an
/// organisation. Mirrors DeactivateBoothEndpoint.</summary>
public sealed class DeactivateOrganisationEndpoint(IAdminOrganisationService service)
    : Endpoint<OrganisationByIdRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/organisations/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Organisations.Delete),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(OrganisationByIdRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await service.DeactivateAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

/// <summary>
/// <c>POST /admin/organisations/import</c> — bulk upsert organisations from a
/// government XLSX workbook upload (Organisation lookup module). Multipart form
/// upload, single file field "file". Returns the per-row outcome summary.
///
/// <para>Defence-in-depth (mirrors ImportUsersEndpoint): the upload is capped
/// at 5 MB and the first four bytes must be the ZIP magic <c>50 4B 03 04</c>
/// (every .xlsx workbook starts with these). ClosedXML is itself a Zip handler;
/// without these gates a hostile admin could submit a Zip-bomb workbook and OOM
/// the API.</para>
/// </summary>
public sealed class ImportOrganisationsEndpoint(IAdminOrganisationService service)
    : Endpoint<EmptyRequest, ApiResult<OrganisationImportResult>>
{
    /// <summary>Maximum accepted upload size in bytes (5 MB).</summary>
    private const long MaxUploadBytes = 5L * 1024 * 1024;

    /// <summary>ZIP local-file-header magic — every .xlsx workbook starts with these four bytes.</summary>
    private static readonly byte[] ZipMagic = { 0x50, 0x4B, 0x03, 0x04 };

    public override void Configure()
    {
        Post("/admin/organisations/import");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Organisations.Import),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
        AllowFileUploads();
        Summary(summary => summary.Summary =
            "Import organisations from an XLSX workbook. Requires the Organisations.Import permission.");
    }

    public override async Task HandleAsync(EmptyRequest _, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

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
                ErrorCodes.OrganisationImportFailed, 413,
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

        using var workbook = new MemoryStream(bytes, writable: false);
        var result = await service.ImportAsync(actorId, workbook, ct);
        await Send.OkAsync(ApiResult<OrganisationImportResult>.Ok(result), ct);
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

/// <summary>
/// <c>GET /organisations</c> — the public organisation picker search. Binds the
/// <c>search</c> + <c>top</c> (default 20) query params and returns the active
/// lightweight picker items.
///
/// <para>Auth: the Configure() shape is copied verbatim from the
/// profile-upsert / profile endpoints (<c>Tags</c> + the <c>auth</c>
/// rate-limit bucket, no explicit <c>Policies(...)</c>), so the caller must be
/// signed in but is not admin-gated and not approval-gated. <b>Not</b>
/// <c>AllowAnonymous</c>.</para>
/// </summary>
public sealed class OrganisationPickerSearchRequest
{
    /// <summary>Free-text search over the organisation name (Arabic / English); null returns the top rows.</summary>
    public string? Search { get; set; }

    /// <summary>Maximum number of picker items to return. Defaults to 20.</summary>
    public int Top { get; set; } = 20;
}

public sealed class OrganisationPickerSearchEndpoint(IPublicOrganisationService service)
    : Endpoint<OrganisationPickerSearchRequest, ApiResult<IReadOnlyList<OrganisationPickerItem>>>
{
    public override void Configure()
    {
        Get("/app/organisations");
        Tags("Account");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Search active organisations for the picker (requires sign-in).");
    }

    public override async Task HandleAsync(
        OrganisationPickerSearchRequest req, CancellationToken ct)
    {
        var items = await service.SearchAsync(req.Search, req.Top, ct);
        await Send.OkAsync(
            ApiResult<IReadOnlyList<OrganisationPickerItem>>.Ok(items), ct);
    }
}
