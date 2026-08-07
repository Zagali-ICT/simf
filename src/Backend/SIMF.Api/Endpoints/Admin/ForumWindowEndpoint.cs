// Tests: SIMF.Api.Tests/BusinessMeetingsTests.cs
using FastEndpoints;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Programme;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>The forum-day window (MIN/MAX over the active
/// <c>ProgrammeDay.Date</c> rows) used by the CP meeting-scheduling pages to bound
/// their date pickers to the event days. A read-only convenience endpoint gated by
/// the existing <see cref="PermissionCatalog.BusinessMeetings"/> View permission
/// (the primary consumer is the Business Meetings page); no new permission code is
/// introduced. Returns both dates as <c>null</c> when no programme days exist.</summary>
public sealed class GetForumWindowEndpoint(IForumWindowService service)
    : EndpointWithoutRequest<ApiResult<ForumWindowResponse>>
{
    public override void Configure()
    {
        Get("/admin/programme/forum-window");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.BusinessMeetings.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var window = await service.GetForumDaysAsync(ct);
        var response = window is { } w
            ? new ForumWindowResponse(w.MinDate, w.MaxDate)
            : new ForumWindowResponse(null, null);
        await Send.OkAsync(ApiResult<ForumWindowResponse>.Ok(response), ct);
    }
}
