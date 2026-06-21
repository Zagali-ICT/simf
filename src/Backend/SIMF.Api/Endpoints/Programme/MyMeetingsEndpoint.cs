// Tests: SIMF.Api.Tests/MyMeetingsTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.MeetingRequests.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Programme;

namespace SIMF.Api.Endpoints.Programme;

/// <summary>D-479 (#11 follow-up) — the mobile read-only "My meetings" feed: the
/// meetings the signed-in user requested (speaker + delegation), newest first.
/// Approved-account only; the user only ever sees their own meetings.</summary>
public sealed class MyMeetingsEndpoint(IMyMeetingsService service)
    : EndpointWithoutRequest<ApiResult<IReadOnlyList<MyMeetingItem>>>
{
    public override void Configure()
    {
        Get("/app/my-meetings");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Meetings");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<IReadOnlyList<MyMeetingItem>>.Ok(
            await service.GetMyMeetingsAsync(userId, ct)), ct);
    }
}
