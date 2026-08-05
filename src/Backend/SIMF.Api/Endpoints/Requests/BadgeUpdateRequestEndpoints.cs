// Tests: SIMF.Api.Tests/BadgeUpdateRequestsTests.cs
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Api.RequestContext;
using SIMF.Application.Requests.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Requests;

namespace SIMF.Api.Endpoints.Requests;

/// <summary>D-500 (Wave 5, الطلبات "طلب تحديث البادج") — an approved attendee
/// submits a badge job-title update request. Login-required.</summary>
public sealed class SubmitBadgeUpdateRequestEndpoint(IBadgeUpdateRequestService service)
    : Endpoint<SubmitBadgeUpdateRequestBody, ApiResult<BadgeUpdateRequestSubmitted>>
{
    public override void Configure()
    {
        Post("/app/badge-requests");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Requests");
    }

    public override async Task HandleAsync(SubmitBadgeUpdateRequestBody req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<BadgeUpdateRequestSubmitted>.Ok(
            await service.SubmitAsync(actorId, req, ct)), ct);
    }
}

// -- Admin badge-update-request management --

public sealed class ListAdminBadgeUpdateRequestsEndpoint(IBadgeUpdateRequestService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminBadgeUpdateRequestRow>>>
{
    public override void Configure()
    {
        Post("/admin/badge-requests/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.BadgeUpdateRequests.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<GridPage<AdminBadgeUpdateRequestRow>>.Ok(
            await service.ListAllAsync(actorId, req, ct)), ct);
    }
}

public sealed class GetAdminBadgeUpdateRequestRoute
{
    public Guid Id { get; set; }
}

public sealed class GetAdminBadgeUpdateRequestEndpoint(IBadgeUpdateRequestService service)
    : Endpoint<GetAdminBadgeUpdateRequestRoute, ApiResult<AdminBadgeUpdateRequestDetail>>
{
    public override void Configure()
    {
        Get("/admin/badge-requests/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.BadgeUpdateRequests.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(GetAdminBadgeUpdateRequestRoute req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<AdminBadgeUpdateRequestDetail>.Ok(
            await service.GetAsync(actorId, req.Id, ct)), ct);
    }
}

public sealed class RespondToBadgeUpdateRequestRoute : RespondToBadgeUpdateRequestRequest
{
    public Guid Id { get; set; }
}

public sealed class RespondToBadgeUpdateRequestEndpoint(IBadgeUpdateRequestService service)
    : Endpoint<RespondToBadgeUpdateRequestRoute, ApiResult<AdminBadgeUpdateRequestDetail>>
{
    public override void Configure()
    {
        Put("/admin/badge-requests/{id:guid}/respond");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.BadgeUpdateRequests.Manage),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(RespondToBadgeUpdateRequestRoute req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<AdminBadgeUpdateRequestDetail>.Ok(
            await service.RespondAsync(actorId, req.Id, req, ct)), ct);
    }
}
