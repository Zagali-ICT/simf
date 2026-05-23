// Tests: SIMF.Api.Tests/VisitorProfileTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.VisitorProfile;

namespace SIMF.Api.Endpoints.Account;

/// <summary>
/// <c>GET /api/v1/account/visitor-profile</c> — returns the actor's visitor
/// profile (decision D-046 b). Auth required; the actor reads their own
/// row only.
/// </summary>
public sealed class VisitorProfileGetEndpoint(IVisitorProfileService service)
    : EndpointWithoutRequest<ApiResult<VisitorProfileResponse>>
{
    public override void Configure()
    {
        Get("/account/visitor-profile");
        Tags("Account");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Return the signed-in visitor's profile.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var response = await service.GetMineAsync(actorId, ct);
        await Send.OkAsync(ApiResult<VisitorProfileResponse>.Ok(response), ct);
    }
}
