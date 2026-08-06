// Tests: SIMF.Api.Tests/AdminSessionModeratorsTests.cs
using FastEndpoints;
using SIMF.Api.RequestContext;
using SIMF.Application.SessionQuestions.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>Admin CRUD over per-session moderator
/// grants. AdministratorOnly — admins assign, moderators do not
/// self-promote.</summary>
public sealed class ListSessionModeratorsEndpoint(IAdminSessionModeratorService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminSessionModeratorRow>>>
{
    public override void Configure()
    {
        Post("/admin/session-moderators/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SessionModerators.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminSessionModeratorRow>>.Ok(
            await service.ListAllAsync(req, ct)), ct);
}

/// <summary>DEF-MOD-005 — the assign dialog's two pickers (active sessions +
/// eligible accounts). Gated by the same <c>SessionModerators.Assign</c>
/// permission as the write it feeds, so whoever may assign a moderator can
/// always reach the lookups.</summary>
public sealed class ListSessionModeratorAssignOptionsEndpoint(IAdminSessionModeratorService service)
    : EndpointWithoutRequest<ApiResult<SessionModeratorAssignOptions>>
{
    public override void Configure()
    {
        Get("/admin/session-moderators/assign-options");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SessionModerators.Assign),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<SessionModeratorAssignOptions>.Ok(
            await service.ListAssignOptionsAsync(ct)), ct);
}

public sealed class AssignSessionModeratorEndpoint(IAdminSessionModeratorService service)
    : Endpoint<AssignSessionModeratorRequest, ApiResult<AdminSessionModeratorRow>>
{
    public override void Configure()
    {
        Post("/admin/session-moderators");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SessionModerators.Assign),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(AssignSessionModeratorRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<AdminSessionModeratorRow>.Ok(
            await service.AssignAsync(actorId, req, ct)), ct);
    }
}

public sealed class RevokeSessionModeratorRoute
{
    public Guid SessionId { get; set; }
    public Guid UserId { get; set; }
}

public sealed class RevokeSessionModeratorEndpoint(IAdminSessionModeratorService service)
    : Endpoint<RevokeSessionModeratorRoute, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/session-moderators/{sessionId:guid}/{userId:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SessionModerators.Revoke),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(RevokeSessionModeratorRoute req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await service.RevokeAsync(actorId, req.SessionId, req.UserId, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}
