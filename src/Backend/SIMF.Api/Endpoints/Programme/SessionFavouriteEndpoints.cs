// Tests: SIMF.Api.Tests/SessionFavouriteTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;

namespace SIMF.Api.Endpoints.Programme;

/// <summary>Add the caller's favourite (المفضلة) on a session — the heart on the
/// session-summaries (1388:8392) + my-sessions (1388:9067) screens. Idempotent;
/// approved account. A 404 if the session is unknown/inactive.</summary>
public sealed class FavouriteSessionEndpoint(ISessionFavouriteService service)
    : EndpointWithoutRequest<ApiResult<bool>>
{
    public override void Configure()
    {
        Post("/app/sessions/{id:guid}/favourite");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Sessions");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await service.AddAsync(userId, Route<Guid>("id"), ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

/// <summary>Remove the caller's favourite on a session. Idempotent.</summary>
public sealed class UnfavouriteSessionEndpoint(ISessionFavouriteService service)
    : EndpointWithoutRequest<ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/app/sessions/{id:guid}/favourite");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Sessions");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await service.RemoveAsync(userId, Route<Guid>("id"), ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

/// <summary>The caller's favourited session ids — the client marks hearts +
/// drives the المفضلة filter from this set. Approved account.</summary>
public sealed class MyFavouriteSessionsEndpoint(ISessionFavouriteService service)
    : EndpointWithoutRequest<ApiResult<IReadOnlyList<Guid>>>
{
    public override void Configure()
    {
        Get("/app/sessions/favourites");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Sessions");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(
            ApiResult<IReadOnlyList<Guid>>.Ok(await service.ListMineAsync(userId, ct)), ct);
    }
}
