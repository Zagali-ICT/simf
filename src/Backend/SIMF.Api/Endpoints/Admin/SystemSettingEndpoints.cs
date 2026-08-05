// Tests: SIMF.Api.Tests/SystemSettingsTests.cs
using FastEndpoints;
using SIMF.Api.RequestContext;
using SIMF.Application.Configuration.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>P2.4 — D-229 (SIMF-FDS-012 §5.5): System Configuration settings
/// CRUD. Gated by Configuration.*. The store ships empty; the team seeds the
/// keys (FDS-012 OI-2).</summary>
public sealed class ListSystemSettingsEndpoint(IAdminSystemSettingService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminSystemSettingSummary>>>
{
    public override void Configure()
    {
        Post("/admin/system-settings/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Configuration.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminSystemSettingSummary>>.Ok(
            await service.ListAsync(req, ct)), ct);
}

public sealed class GetSystemSettingRequest { public Guid Id { get; set; } }

public sealed class GetSystemSettingEndpoint(IAdminSystemSettingService service)
    : Endpoint<GetSystemSettingRequest, ApiResult<AdminSystemSettingDetail>>
{
    public override void Configure()
    {
        Get("/admin/system-settings/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Configuration.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(GetSystemSettingRequest req, CancellationToken ct)
    {
        var detail = await service.GetAsync(req.Id, ct)
            ?? throw new ApiException(
                ErrorCodes.SystemSettingNotFound, 404,
                "The system setting was not found.",
                "لم يتم العثور على الإعداد.");
        await Send.OkAsync(ApiResult<AdminSystemSettingDetail>.Ok(detail), ct);
    }
}

public sealed class CreateSystemSettingEndpoint(IAdminSystemSettingService service)
    : Endpoint<AdminCreateSystemSettingRequest, ApiResult<AdminSystemSettingDetail>>
{
    public override void Configure()
    {
        Post("/admin/system-settings");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Configuration.Create),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(AdminCreateSystemSettingRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<AdminSystemSettingDetail>.Ok(
            await service.CreateAsync(actorId, req, ct)), ct);
    }
}

public sealed class UpdateSystemSettingEndpoint(IAdminSystemSettingService service)
    : Endpoint<AdminUpdateSystemSettingRequest, ApiResult<AdminSystemSettingDetail>>
{
    public override void Configure()
    {
        Put("/admin/system-settings/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Configuration.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(AdminUpdateSystemSettingRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        var id = Route<Guid>("id");
        await Send.OkAsync(ApiResult<AdminSystemSettingDetail>.Ok(
            await service.UpdateAsync(actorId, id, req, ct)), ct);
    }
}

public sealed class DeleteSystemSettingRequest { public Guid Id { get; set; } }

public sealed class DeleteSystemSettingEndpoint(IAdminSystemSettingService service)
    : Endpoint<DeleteSystemSettingRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/system-settings/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Configuration.Delete),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(DeleteSystemSettingRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await service.DeactivateAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}
