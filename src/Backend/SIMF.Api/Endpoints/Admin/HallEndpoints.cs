// Tests: SIMF.Api.Tests/AdminHallsTests.cs
// Tests: SIMF.Api.Tests/HallScheduleTests.cs (QA B16 — active-only occupancy)
using FastEndpoints;
using SIMF.Api.RequestContext;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

public sealed class ListHallsEndpoint(IAdminHallService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminHallSummary>>>
{
    public override void Configure()
    {
        Post("/admin/halls/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Halls.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(GridQuery req, CancellationToken ct)
    {
        await Send.OkAsync(ApiResult<GridPage<AdminHallSummary>>.Ok(
            await service.ListAllAsync(req, ct)), ct);
    }
}

public sealed class GetHallRequest { public Guid Id { get; set; } }

public sealed class GetHallEndpoint(IAdminHallService service)
    : Endpoint<GetHallRequest, ApiResult<AdminHallDetail>>
{
    public override void Configure()
    {
        Get("/admin/halls/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Halls.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(GetHallRequest req, CancellationToken ct)
    {
        var detail = await service.GetAsync(req.Id, ct)
            ?? throw new ApiException(ErrorCodes.HallNotFound, 404,
                "The hall was not found.",
                "لم يتم العثور على القاعة.");
        await Send.OkAsync(ApiResult<AdminHallDetail>.Ok(detail), ct);
    }
}

/// <summary>QA B16 — the hall's own occupancy view: the sessions assigned to this
/// hall, so an admin can see what a hall is doing instead of only meeting the
/// booking-overlap 409 after the fact. Reuses <c>IAdminSessionService.ListAllAsync</c>
/// (it already filters on the <c>hallId</c> grid filter) rather than adding a second
/// query over the same table, and carries the <c>Halls.View</c> gate the hall detail
/// surface already requires.</summary>
public sealed class GetHallScheduleEndpoint(IAdminSessionService sessions)
    : EndpointWithoutRequest<ApiResult<GridPage<AdminSessionSummary>>>
{
    /// <summary>How many sessions the hall schedule returns. A hall runs a handful
    /// of sessions across the forum, so one page is the whole schedule. This is
    /// also the <c>ClampPage</c> ceiling, so a busier hall WOULD be truncated —
    /// the returned <c>GridPage.Total</c> is the unclamped active count, and the
    /// panel says so rather than passing a partial schedule off as complete.</summary>
    private const int ScheduleRows = 200;

    public override void Configure()
    {
        Get("/admin/halls/{hallId:guid}/schedule");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Halls.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var query = new GridQuery { Top = ScheduleRows };
        query.Filters["hallId"] = Route<Guid>("hallId").ToString();
        // Occupancy means ACTIVE occupancy. ListAllAsync only applies its
        // isActive filter when the caller supplies it, and the panel's Status
        // column shows the SessionStatus lifecycle, not IsActive — so without
        // this a soft-deleted session would render as a live booking. The rule
        // this view exists to expose (EnsureNoHallTimeOverlapAsync) matches on
        // other.IsActive, so the two must agree.
        query.Filters["isActive"] = bool.TrueString;
        await Send.OkAsync(ApiResult<GridPage<AdminSessionSummary>>.Ok(
            await sessions.ListAllAsync(query, ct)), ct);
    }
}

public sealed class CreateHallEndpoint(IAdminHallService service)
    : Endpoint<AdminCreateHallRequest, ApiResult<AdminHallDetail>>
{
    public override void Configure()
    {
        Post("/admin/halls");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Halls.Create),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(AdminCreateHallRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<AdminHallDetail>.Ok(
            await service.CreateAsync(actorId, req, ct)), ct);
    }
}

// Bind {id} + body via a derived route that INHERITS the contract
// (mirrors UpdateExhibitorRoute / UpdateSponsorRoute). The old inline
// UpdateHallRequest omitted the GPS geofence fields, so FastEndpoints dropped
// the geofence the CP form sends and UpdateAsync wiped the stored geofence on
// every edit. Passing the bound req straight through makes the drop impossible.
public sealed class UpdateHallRoute : AdminUpdateHallRequest
{
    public Guid Id { get; set; }
}

public sealed class UpdateHallEndpoint(IAdminHallService service)
    : Endpoint<UpdateHallRoute, ApiResult<AdminHallDetail>>
{
    public override void Configure()
    {
        Put("/admin/halls/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Halls.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(UpdateHallRoute req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<AdminHallDetail>.Ok(
            await service.UpdateAsync(actorId, req.Id, req, ct)), ct);
    }
}

public sealed class DeactivateHallRequest { public Guid Id { get; set; } }

public sealed class DeactivateHallEndpoint(IAdminHallService service)
    : Endpoint<DeactivateHallRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/halls/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Halls.Delete),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(DeactivateHallRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await service.DeactivateAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}
