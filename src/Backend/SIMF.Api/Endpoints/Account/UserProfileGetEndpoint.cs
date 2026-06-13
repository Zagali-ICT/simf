// Tests: SIMF.Api.Tests/UserProfileTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.UserProfile;

namespace SIMF.Api.Endpoints.Account;

/// <summary>
/// <c>GET /api/v1/app/account/user-profile</c> — returns the actor's
/// profile (decisions D-046 b, P8 — D-049; renamed from
/// <c>/account/visitor-profile</c>). Auth required; the actor reads
/// their own row only. The unrelated <c>/account/profile</c> endpoint
/// is the lightweight identity-card surface (id / email / avatar / 2FA
/// / roles); the user-profile here is the richer registration form.
/// </summary>
public sealed class UserProfileGetEndpoint(IUserProfileService service)
    : EndpointWithoutRequest<ApiResult<UserProfileResponse>>
{
    public override void Configure()
    {
        Get("/app/account/user-profile");
        Tags("Account");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Return the signed-in user's profile.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var response = await service.GetMineAsync(actorId, ct);
        await Send.OkAsync(ApiResult<UserProfileResponse>.Ok(response), ct);
    }
}
