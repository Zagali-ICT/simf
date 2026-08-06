// Tests: SIMF.Api.Tests/AdminInvitationsTests.cs
using FastEndpoints;
using SIMF.Api.RequestContext;
using SIMF.Application.PublicRelations.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>Admin CRUD over
/// <c>Invitations</c>. Gated by the per-action <c>Invitations.View</c> /
/// <c>Invitations.Manage</c> permissions. The PublicRelations role
/// holds them as seeded baseline grants and Administrator via the wildcard, so
/// both hit the same surface; the service layer is role-agnostic.</summary>
public sealed class ListInvitationsEndpoint(IAdminInvitationService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminInvitationSummary>>>
{
    public override void Configure()
    {
        Post("/admin/invitations/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Invitations.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminInvitationSummary>>.Ok(
            await service.ListAllAsync(req, ct)), ct);
}

public sealed class GetInvitationRequest { public Guid Id { get; set; } }

public sealed class GetInvitationEndpoint(IAdminInvitationService service)
    : Endpoint<GetInvitationRequest, ApiResult<AdminInvitationDetail>>
{
    public override void Configure()
    {
        Get("/admin/invitations/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Invitations.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GetInvitationRequest req, CancellationToken ct)
    {
        var detail = await service.GetAsync(req.Id, ct)
            ?? throw new ApiException(
                ErrorCodes.InvitationNotFound, 404,
                "The invitation was not found.",
                "لم يتم العثور على الدعوة.");
        await Send.OkAsync(ApiResult<AdminInvitationDetail>.Ok(detail), ct);
    }
}

public sealed class CreateInvitationEndpoint(IAdminInvitationService service)
    : Endpoint<AdminCreateInvitationRequest, ApiResult<AdminInvitationDetail>>
{
    public override void Configure()
    {
        Post("/admin/invitations");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Invitations.Manage),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(AdminCreateInvitationRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<AdminInvitationDetail>.Ok(
            await service.CreateAsync(actorId, req, ct)), ct);
    }
}

/// <summary>PUT request that binds Id from the route and
/// State / Notes from the body. Avoids the duplicate-DTO indirection
/// the earlier shape carried.</summary>
public sealed class AdminUpdateInvitationRouteRequest : AdminUpdateInvitationRequest
{
    public Guid Id { get; set; }
}

public sealed class UpdateInvitationEndpoint(IAdminInvitationService service)
    : Endpoint<AdminUpdateInvitationRouteRequest, ApiResult<AdminInvitationDetail>>
{
    public override void Configure()
    {
        Put("/admin/invitations/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Invitations.Manage),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(AdminUpdateInvitationRouteRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<AdminInvitationDetail>.Ok(
            await service.UpdateAsync(actorId, req.Id, req, ct)), ct);
    }
}

public sealed class DeactivateInvitationRequest { public Guid Id { get; set; } }

public sealed class DeactivateInvitationEndpoint(IAdminInvitationService service)
    : Endpoint<DeactivateInvitationRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/invitations/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Invitations.Manage),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(DeactivateInvitationRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await service.DeactivateAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}
