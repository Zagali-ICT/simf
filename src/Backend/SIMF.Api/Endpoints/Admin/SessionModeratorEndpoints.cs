// Tests: SIMF.Api.Tests/AdminSessionModeratorsTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.SessionQuestions.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>D-169 (gap doc G6) — admin CRUD over per-session moderator
/// grants. AdministratorOnly — admins assign, moderators do not
/// self-promote.</summary>
public sealed class ListSessionModeratorsEndpoint(IAdminSessionModeratorService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminSessionModeratorRow>>>
{
    public override void Configure()
    {
        Post("/admin/session-moderators/list");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminSessionModeratorRow>>.Ok(
            await service.ListAllAsync(req, ct)), ct);
}

public sealed class AssignSessionModeratorEndpoint(IAdminSessionModeratorService service)
    : Endpoint<AssignSessionModeratorRequest, ApiResult<AdminSessionModeratorRow>>
{
    public override void Configure()
    {
        Post("/admin/session-moderators");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(AssignSessionModeratorRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
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
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(RevokeSessionModeratorRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await service.RevokeAsync(actorId, req.SessionId, req.UserId, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}
