// Tests: SIMF.Api.Tests/SeatReservationsTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.SeatReservations.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Sessions;

namespace SIMF.Api.Endpoints.Sessions;

/// <summary>D-175 (gap doc G11, Mockup page 7) — authenticated visitor
/// reads the current seat-grid for a session: row labels, the
/// occupied cells, their own active seat (if any), and the active
/// reservation count.</summary>
public sealed class GetSessionSeatMapRoute { public Guid SessionId { get; set; } }

public sealed class GetSessionSeatMapEndpoint(ISeatReservationService service)
    : Endpoint<GetSessionSeatMapRoute, ApiResult<SessionSeatMap>>
{
    public override void Configure()
    {
        Get("/app/sessions/{sessionId:guid}/seats");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Sessions");
    }
    public override async Task HandleAsync(GetSessionSeatMapRoute req, CancellationToken ct)
    {
        Guid? actorId = Guid.TryParse(User.FindFirstValue("sub"), out var parsed)
            ? parsed : null;
        await Send.OkAsync(ApiResult<SessionSeatMap>.Ok(
            await service.GetSessionSeatMapAsync(req.SessionId, actorId, ct)), ct);
    }
}

public sealed class ReserveSeatRoute : ReserveSeatRequest
{
    public Guid SessionId { get; set; }
}

public sealed class ReserveSeatEndpoint(ISeatReservationService service)
    : Endpoint<ReserveSeatRoute, ApiResult<MySeatReservation>>
{
    public override void Configure()
    {
        Post("/app/sessions/{sessionId:guid}/seats/reserve");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Sessions");
    }
    public override async Task HandleAsync(ReserveSeatRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<MySeatReservation>.Ok(
            await service.ReserveAsync(req.SessionId, actorId,
                new ReserveSeatRequest
                {
                    RowLabel = req.RowLabel,
                    SeatNumber = req.SeatNumber,
                }, ct)), ct);
    }
}

public sealed class ReserveRandomSeatRoute { public Guid SessionId { get; set; } }

public sealed class ReserveRandomSeatEndpoint(ISeatReservationService service)
    : Endpoint<ReserveRandomSeatRoute, ApiResult<MySeatReservation>>
{
    public override void Configure()
    {
        Post("/app/sessions/{sessionId:guid}/seats/reserve-random");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Sessions");
    }
    public override async Task HandleAsync(ReserveRandomSeatRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<MySeatReservation>.Ok(
            await service.ReserveRandomAsync(req.SessionId, actorId, ct)), ct);
    }
}

public sealed class ReleaseMySeatRoute { public Guid SessionId { get; set; } }

public sealed class ReleaseMySeatEndpoint(ISeatReservationService service)
    : Endpoint<ReleaseMySeatRoute, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/app/sessions/{sessionId:guid}/seats/mine");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Sessions");
    }
    public override async Task HandleAsync(ReleaseMySeatRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await service.ReleaseMineAsync(req.SessionId, actorId, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

// -- Admin --

public sealed class GetHallSeatLayoutRoute { public Guid HallId { get; set; } }

public sealed class GetHallSeatLayoutEndpoint(ISeatReservationService service)
    : Endpoint<GetHallSeatLayoutRoute, ApiResult<HallSeatLayoutSnapshot>>
{
    public override void Configure()
    {
        Get("/admin/halls/{hallId:guid}/seat-layout");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SeatLayouts.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(GetHallSeatLayoutRoute req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<HallSeatLayoutSnapshot>.Ok(
            await service.GetLayoutAsync(req.HallId, ct)), ct);
}

public sealed class SetHallSeatLayoutRoute : SetHallSeatLayoutRequest
{
    public Guid HallId { get; set; }
}

public sealed class SetHallSeatLayoutEndpoint(ISeatReservationService service)
    : Endpoint<SetHallSeatLayoutRoute, ApiResult<HallSeatLayoutSnapshot>>
{
    public override void Configure()
    {
        Put("/admin/halls/{hallId:guid}/seat-layout");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SeatLayouts.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(SetHallSeatLayoutRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<HallSeatLayoutSnapshot>.Ok(
            await service.SetLayoutAsync(actorId, req.HallId,
                new SetHallSeatLayoutRequest
                {
                    RowLabels = req.RowLabels,
                    SeatsPerRow = req.SeatsPerRow,
                }, ct)), ct);
    }
}

public sealed class AdminReserveRowRoute : AdminReserveRowRequest
{
    public Guid SessionId { get; set; }
}

public sealed class AdminReserveRowEndpoint(ISeatReservationService service)
    : Endpoint<AdminReserveRowRoute, ApiResult<bool>>
{
    public override void Configure()
    {
        Post("/admin/sessions/{sessionId:guid}/seats/reserve-row");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SeatPlans.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(AdminReserveRowRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await service.AdminReserveRowAsync(actorId, req.SessionId,
            new AdminReserveRowRequest { RowLabel = req.RowLabel }, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

public sealed class AdminReleaseSeatRoute
{
    public Guid SessionId { get; set; }
    public Guid Id { get; set; }
}

public sealed class AdminReleaseSeatEndpoint(ISeatReservationService service)
    : Endpoint<AdminReleaseSeatRoute, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/sessions/{sessionId:guid}/seats/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SeatPlans.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(AdminReleaseSeatRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await service.AdminReleaseAsync(actorId, req.SessionId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

public sealed class ListSessionSeatReservationsRoute
{
    public Guid SessionId { get; set; }
    public int Skip { get; set; }
    public int Top { get; set; }
    public string? Search { get; set; }
    public string? Sort { get; set; }
    public bool SortDescending { get; set; }
    public Dictionary<string, string> Filters { get; set; } = new();
}

public sealed class ListSessionSeatReservationsEndpoint(ISeatReservationService service)
    : Endpoint<ListSessionSeatReservationsRoute, ApiResult<GridPage<SessionSeatCell>>>
{
    public override void Configure()
    {
        Post("/admin/sessions/{sessionId:guid}/seats/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SeatPlans.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(
        ListSessionSeatReservationsRoute req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<SessionSeatCell>>.Ok(
            await service.ListSessionReservationsAsync(
                req.SessionId,
                new GridQuery
                {
                    Skip = req.Skip,
                    Top = req.Top,
                    Search = req.Search,
                    Sort = req.Sort,
                    SortDescending = req.SortDescending,
                    Filters = req.Filters,
                }, ct)), ct);
}

// -- Booking approval queue (P2.2 / D-227 — FDS-005 §5.2) --

/// <summary>P2.2 — the Control Panel booking approval queue: Pending, held
/// visitor bookings across all sessions.</summary>
public sealed class ListPendingBookingsEndpoint(ISeatReservationService service)
    : Endpoint<GridQuery, ApiResult<GridPage<BookingQueueRow>>>
{
    public override void Configure()
    {
        Post("/admin/bookings/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Bookings.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<BookingQueueRow>>.Ok(
            await service.ListPendingBookingsAsync(req, ct)), ct);
}

public sealed class ApproveBookingRoute { public Guid Id { get; set; } }

public sealed class ApproveBookingEndpoint(ISeatReservationService service)
    : Endpoint<ApproveBookingRoute, ApiResult<bool>>
{
    public override void Configure()
    {
        Post("/admin/bookings/{id:guid}/approve");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Bookings.Approve),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(ApproveBookingRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await service.ApproveBookingAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

public sealed class RejectBookingRoute : RejectBookingRequest
{
    public Guid Id { get; set; }
}

public sealed class RejectBookingEndpoint(ISeatReservationService service)
    : Endpoint<RejectBookingRoute, ApiResult<bool>>
{
    public override void Configure()
    {
        Post("/admin/bookings/{id:guid}/reject");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Bookings.Reject),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(RejectBookingRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await service.RejectBookingAsync(actorId, req.Id,
            new RejectBookingRequest { Reason = req.Reason }, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

public sealed class BulkApproveBookingsRequest
{
    public IReadOnlyList<Guid> ReservationIds { get; set; } = Array.Empty<Guid>();
}

public sealed class BulkApproveBookingsEndpoint(ISeatReservationService service)
    : Endpoint<BulkApproveBookingsRequest, ApiResult<int>>
{
    public override void Configure()
    {
        Post("/admin/bookings/bulk-approve");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Bookings.Approve),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(BulkApproveBookingsRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var approved = await service.BulkApproveBookingsAsync(actorId, req.ReservationIds, ct);
        await Send.OkAsync(ApiResult<int>.Ok(approved), ct);
    }
}
