// Tests: SIMF.Api.Tests/MyRequestsTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.Requests.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Requests;

namespace SIMF.Api.Endpoints.Requests;

/// <summary>D-500 (Wave 5, الطلبات 1408:9726) — the mobile unified "My requests"
/// feed: every request the signed-in user submitted (speaker / delegation /
/// session-attendance / participation-document / badge-update), newest first.
/// Approved-account only; the user only ever sees their own requests.</summary>
public sealed class MyRequestsEndpoint(IMyRequestsService service)
    : EndpointWithoutRequest<ApiResult<IReadOnlyList<AppRequestItem>>>
{
    public override void Configure()
    {
        Get("/app/my-requests");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Requests");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<IReadOnlyList<AppRequestItem>>.Ok(
            await service.GetMyRequestsAsync(userId, ct)), ct);
    }
}

/// <summary>D-500 — the requester withdraws one of their own still-pending
/// requests (speaker / document / badge). 404 when not found / not owned; 409
/// when the kind is not self-cancellable or it is no longer Pending.</summary>
public sealed class CancelMyRequestEndpoint(IMyRequestsService service)
    : Endpoint<CancelMyRequestBody, ApiResult<bool>>
{
    public override void Configure()
    {
        Post("/app/my-requests/cancel");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Requests");
    }

    public override async Task HandleAsync(CancelMyRequestBody req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await service.CancelAsync(userId, req.Kind, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}
