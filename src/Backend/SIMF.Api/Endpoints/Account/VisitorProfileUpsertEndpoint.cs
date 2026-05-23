// Tests: SIMF.Api.Tests/VisitorProfileTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.VisitorProfile;

namespace SIMF.Api.Endpoints.Account;

/// <summary>
/// <c>POST /api/v1/account/visitor-profile</c> — creates / updates the
/// signed-in visitor's profile (decision D-046 b, myComment #18).
/// </summary>
public sealed class VisitorProfileUpsertEndpoint(IVisitorProfileService service)
    : Endpoint<UpsertVisitorProfileRequest, ApiResult<VisitorProfileResponse>>
{
    public override void Configure()
    {
        Post("/account/visitor-profile");
        Tags("Account");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Create or update the signed-in visitor's profile.");
    }

    public override async Task HandleAsync(
        UpsertVisitorProfileRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var response = await service.UpsertMineAsync(actorId, req, ct);
        await Send.OkAsync(ApiResult<VisitorProfileResponse>.Ok(response), ct);
    }
}
