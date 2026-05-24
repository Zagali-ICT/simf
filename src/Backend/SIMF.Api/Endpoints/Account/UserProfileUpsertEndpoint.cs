// Tests: SIMF.Api.Tests/UserProfileTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.UserProfile;

namespace SIMF.Api.Endpoints.Account;

/// <summary>
/// <c>POST /api/v1/account/user-profile</c> — creates / updates the
/// signed-in user's profile (decisions D-046 b, P8 — D-049; renamed
/// from <c>/account/visitor-profile</c>). The unrelated
/// <c>/account/profile</c> endpoint serves the lightweight identity
/// card; this one carries the richer registration data.
/// </summary>
public sealed class UserProfileUpsertEndpoint(IUserProfileService service)
    : Endpoint<UpsertUserProfileRequest, ApiResult<UserProfileResponse>>
{
    public override void Configure()
    {
        Post("/account/user-profile");
        Tags("Account");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Create or update the signed-in user's profile.");
    }

    public override async Task HandleAsync(
        UpsertUserProfileRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var response = await service.UpsertMineAsync(actorId, req, ct);
        await Send.OkAsync(ApiResult<UserProfileResponse>.Ok(response), ct);
    }
}
