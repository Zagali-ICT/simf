// Tests: SIMF.Api.Tests/ArchiveTests.cs, SIMF.Api.Tests/AdminArchiveTests.cs
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Api.RequestContext;
using SIMF.Application.Archive.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Archive;

namespace SIMF.Api.Endpoints.Archive;

/// <summary>D-199 (Mockup screen 24) — public anonymous list of active
/// archive editions. Returns an empty list when the archive-visibility
/// operations toggle (D-166) is off; the gate lives in the service.</summary>
public sealed class ListPublicArchiveEndpoint(IPublicArchiveService service)
    : EndpointWithoutRequest<ApiResult<PublicArchive>>
{
    public override void Configure()
    {
        Get("/app/archive");
        AllowAnonymous();
        Tags("Public");
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<PublicArchive>.Ok(
            await service.ListAsync(ct)), ct);
}

public sealed class GetPublicArchiveEditionRoute { public Guid Id { get; set; } }

/// <summary>§9 (Mockup screen 24-01 "تفاصيل النسخة") — public anonymous detail
/// for one past edition. Gated by the archive-visibility toggle (D-166): 404
/// when the archive is hidden, the edition is missing, or it is inactive.</summary>
public sealed class GetPublicArchiveEditionEndpoint(IPublicArchiveService service)
    : Endpoint<GetPublicArchiveEditionRoute, ApiResult<PublicArchiveEditionDetail>>
{
    public override void Configure()
    {
        Get("/app/archive/{id:guid}");
        AllowAnonymous();
        Tags("Public");
    }

    public override async Task HandleAsync(
        GetPublicArchiveEditionRoute req, CancellationToken ct)
    {
        var detail = await service.GetAsync(req.Id, ct)
            ?? throw new ApiException("archive_edition_not_found", 404,
                "The archive edition was not found.",
                "لم يتم العثور على نسخة الأرشيف.");
        await Send.OkAsync(ApiResult<PublicArchiveEditionDetail>.Ok(detail), ct);
    }
}

// -- Admin archive edition CRUD --

public sealed class ListAdminArchiveEndpoint(IAdminArchiveService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminArchiveEditionSummary>>>
{
    public override void Configure()
    {
        Post("/admin/archive/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Archive.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminArchiveEditionSummary>>.Ok(
            await service.ListAllAsync(req, ct)), ct);
}

public sealed class GetArchiveEditionRoute { public Guid Id { get; set; } }

public sealed class GetArchiveEditionEndpoint(IAdminArchiveService service)
    : Endpoint<GetArchiveEditionRoute, ApiResult<AdminArchiveEditionDetail>>
{
    public override void Configure()
    {
        Get("/admin/archive/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Archive.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(
        GetArchiveEditionRoute req, CancellationToken ct)
    {
        var detail = await service.GetAsync(req.Id, ct)
            ?? throw new ApiException("archive_edition_not_found", 404,
                "The archive edition was not found.",
                "لم يتم العثور على نسخة الأرشيف.");
        await Send.OkAsync(ApiResult<AdminArchiveEditionDetail>.Ok(detail), ct);
    }
}

public sealed class CreateArchiveEditionEndpoint(IAdminArchiveService service)
    : Endpoint<CreateArchiveEditionRequest, ApiResult<AdminArchiveEditionDetail>>
{
    public override void Configure()
    {
        Post("/admin/archive");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Archive.Create),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(
        CreateArchiveEditionRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<AdminArchiveEditionDetail>.Ok(
            await service.CreateAsync(actorId, req, ct)), ct);
    }
}

public sealed record UpdateArchiveEditionRoute : UpdateArchiveEditionRequest
{
    public Guid Id { get; set; }
}

public sealed class UpdateArchiveEditionEndpoint(IAdminArchiveService service)
    : Endpoint<UpdateArchiveEditionRoute, ApiResult<AdminArchiveEditionDetail>>
{
    public override void Configure()
    {
        Put("/admin/archive/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Archive.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(
        UpdateArchiveEditionRoute req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<AdminArchiveEditionDetail>.Ok(
            await service.UpdateAsync(actorId, req.Id, req, ct)), ct);
    }
}

public sealed class DeleteArchiveEditionRoute { public Guid Id { get; set; } }

public sealed class DeleteArchiveEditionEndpoint(IAdminArchiveService service)
    : Endpoint<DeleteArchiveEditionRoute, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/archive/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Archive.Delete),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(
        DeleteArchiveEditionRoute req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await service.DeactivateAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

/// <summary>D-275 (§9) — "make this year history": one-click snapshot of the
/// current live event into a new archive edition. Year + bilingual title are
/// generated, and the counters (attendees = distinct gate-scan arrivals,
/// sessions, speakers) are computed server-side. The optional
/// <c>MakeVisible</c> flips the archive-visibility toggle (D-166) on.</summary>
public sealed class SnapshotCurrentArchiveEndpoint(IAdminArchiveService service)
    : Endpoint<SnapshotCurrentEditionRequest, ApiResult<AdminArchiveEditionDetail>>
{
    public override void Configure()
    {
        Post("/admin/archive/snapshot-current");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Archive.Snapshot),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(
        SnapshotCurrentEditionRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<AdminArchiveEditionDetail>.Ok(
            await service.SnapshotCurrentAsync(actorId, req, ct)), ct);
    }
}
