// Tests: SIMF.Api.Tests/EventEditionTests.cs
using FastEndpoints;
using SIMF.Api.RequestContext;
using SIMF.Application.Editions.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>GET /api/v1/admin/editions/current</c> — the year currently open.
/// </summary>
public sealed class GetCurrentEditionEndpoint(IEventEditionService editions)
    : EndpointWithoutRequest<ApiResult<AdminEventEditionResponse>>
{
    public override void Configure()
    {
        Get("/admin/editions/current");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Editions.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary = "The event edition currently open.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var state = await editions.GetAsync(ct);
        await Send.OkAsync(
            ApiResult<AdminEventEditionResponse>.Ok(new AdminEventEditionResponse(
                state.Year, state.OpenedAt, state.LastClosedAt, state.LastReissueCount)),
            ct);
    }
}

/// <summary>
/// <c>POST /api/v1/admin/editions/open</c> — closes the current year into
/// history and opens the given one.
///
/// <para>Gated on <c>Editions.Open</c> rather than the read code, because this
/// clears every attendee's badge: the forum's whole population has to collect a
/// new one afterwards, which is not an authority a viewer should hold.</para>
/// </summary>
public sealed class OpenEditionEndpoint(IEventEditionService editions)
    : Endpoint<AdminOpenEditionRequest, ApiResult<AdminOpenEditionResponse>>
{
    public override void Configure()
    {
        Post("/admin/editions/open");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Editions.Open), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Close the current event edition into history and open a new year. Clears every badge for re-issue.");
    }

    public override async Task HandleAsync(AdminOpenEditionRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        var result = await editions.OpenYearAsync(actorId, req.Year, ct);
        await Send.OkAsync(
            ApiResult<AdminOpenEditionResponse>.Ok(
                new AdminOpenEditionResponse(result.Year, result.BadgesCleared)),
            ct);
    }
}
