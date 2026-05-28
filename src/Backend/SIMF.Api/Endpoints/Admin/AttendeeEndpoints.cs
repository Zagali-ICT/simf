// Tests: SIMF.Api.Tests/AdminAttendeesTests.cs
using FastEndpoints;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// D-134 Sprint A — read-only attendee roster across Visitors + Others.
/// Administrator-only; no rate-limit (read endpoint).
/// </summary>
public sealed class ListAttendeesEndpoint(IAdminAttendeeService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminAttendeeSummary>>>
{
    public override void Configure()
    {
        Post("/admin/attendees/list");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Return one page of the attendee roster (Visitors + Others). " +
            "Requires Administrator role.");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct)
    {
        var page = await service.ListAsync(req, ct);
        await Send.OkAsync(ApiResult<GridPage<AdminAttendeeSummary>>.Ok(page), ct);
    }
}
