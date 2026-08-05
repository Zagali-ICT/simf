// Tests: SIMF.Api.Tests/ArchiveVisibilityTests.cs
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Api.RequestContext;
using SIMF.Application.Operations.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Operations;

/// <summary>D-166 (gap doc G4, PDF §2.4) — public GET so the Flutter
/// app + Website can decide whether to show the past-events archive
/// without authenticating first.</summary>
public sealed class GetArchiveVisibilityPublicEndpoint(IOperationsToggleService service)
    : EndpointWithoutRequest<ApiResult<ArchiveVisibilityState>>
{
    public override void Configure()
    {
        Get("/app/archive/visibility");
        AllowAnonymous();
        Tags("Public");
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<ArchiveVisibilityState>.Ok(
            await service.GetArchiveVisibilityAsync(ct)), ct);
}

public sealed class GetArchiveVisibilityAdminEndpoint(IOperationsToggleService service)
    : EndpointWithoutRequest<ApiResult<ArchiveVisibilityState>>
{
    public override void Configure()
    {
        Get("/admin/archive/visibility");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Operations.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<ArchiveVisibilityState>.Ok(
            await service.GetArchiveVisibilityAsync(ct)), ct);
}

public sealed class UpdateArchiveVisibilityEndpoint(IOperationsToggleService service)
    : Endpoint<UpdateArchiveVisibilityRequest, ApiResult<ArchiveVisibilityState>>
{
    public override void Configure()
    {
        Put("/admin/archive/visibility");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Operations.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(
        UpdateArchiveVisibilityRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<ArchiveVisibilityState>.Ok(
            await service.UpdateArchiveVisibilityAsync(actorId, req, ct)), ct);
    }
}
