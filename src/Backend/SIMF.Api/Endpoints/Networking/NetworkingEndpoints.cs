// Tests: SIMF.Api.Tests/NetworkingTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.Networking.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Networking;

namespace SIMF.Api.Endpoints.Networking;

/// <summary>B6 — D-224: visitor-to-visitor networking connections. All
/// endpoints are app-facing — gated by RequireApprovedAccount only (no
/// permission code), under the caller's own <c>/account/connections</c>
/// resource, mirroring MeetPeopleLikeYou. The caller is the JWT <c>sub</c>.</summary>
public sealed class SendConnectionEndpoint(INetworkingService service)
    : Endpoint<SendConnectionRequest, ApiResult<ConnectionResult>>
{
    public override void Configure()
    {
        Post("/account/connections");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Account");
    }

    public override async Task HandleAsync(SendConnectionRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var callerId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var result = await service.RequestAsync(callerId, req.TargetUserId, ct);
        await Send.OkAsync(ApiResult<ConnectionResult>.Ok(result), ct);
    }
}

public sealed class AcceptConnectionRoute { public Guid Id { get; set; } }

public sealed class AcceptConnectionEndpoint(INetworkingService service)
    : Endpoint<AcceptConnectionRoute, ApiResult<ConnectionResult>>
{
    public override void Configure()
    {
        Put("/account/connections/{id:guid}/accept");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Account");
    }

    public override async Task HandleAsync(AcceptConnectionRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var callerId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var result = await service.AcceptAsync(callerId, req.Id, ct);
        await Send.OkAsync(ApiResult<ConnectionResult>.Ok(result), ct);
    }
}

public sealed class RemoveConnectionRoute { public Guid Id { get; set; } }

public sealed class RemoveConnectionEndpoint(INetworkingService service)
    : Endpoint<RemoveConnectionRoute, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/account/connections/{id:guid}");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Account");
    }

    public override async Task HandleAsync(RemoveConnectionRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var callerId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await service.RemoveAsync(callerId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

public sealed class ListConnectionsEndpoint(INetworkingService service)
    : EndpointWithoutRequest<ApiResult<IReadOnlyList<ConnectionRow>>>
{
    public override void Configure()
    {
        Get("/account/connections");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Account");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var callerId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var rows = await service.ListMineAsync(callerId, ct);
        await Send.OkAsync(ApiResult<IReadOnlyList<ConnectionRow>>.Ok(rows), ct);
    }
}
