// Tests: SIMF.Api.Tests/BusinessMeetingsTests.cs
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Api.RequestContext;
using SIMF.Application.BusinessMeetings.Abstractions;
using SIMF.Common;
using SIMF.Contracts.BusinessMeetings;

namespace SIMF.Api.Endpoints.BusinessMeetings;

// SIMF-FDS-013 — D-248: Control Panel endpoints for flexible hall configuration +
// admin-arranged B2B/B2C business meetings. All admin-only, gated by the
// PermissionCatalog policy + RequireApprovedAccount, mirroring BoothEndpoints.

// ── Hall purpose ─────────────────────────────────────────────────────────────

public sealed class SetHallPurposeRoute : SetHallPurposeRequest { public Guid Id { get; set; } }

public sealed class SetHallPurposeEndpoint(IBusinessMeetingService service)
    : Endpoint<SetHallPurposeRoute, ApiResult<bool>>
{
    public override void Configure()
    {
        Put("/admin/halls/{id:guid}/purpose");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Halls.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(SetHallPurposeRoute req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await service.SetHallPurposeAsync(actorId, req.Id,
            new SetHallPurposeRequest { Purpose = req.Purpose }, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

// ── Meeting tables ───────────────────────────────────────────────────────────

public sealed class ListMeetingTablesRequest
{
    public Guid HallId { get; set; }
    public int Skip { get; set; }
    public int Top { get; set; } = 50;
}

public sealed class ListMeetingTablesEndpoint(IBusinessMeetingService service)
    : Endpoint<ListMeetingTablesRequest, ApiResult<GridPage<MeetingTableRow>>>
{
    public override void Configure()
    {
        Post("/admin/halls/{hallId:guid}/meeting-tables/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.MeetingTables.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(ListMeetingTablesRequest req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<MeetingTableRow>>.Ok(
            await service.ListTablesAsync(req.HallId,
                new GridQuery { Skip = req.Skip, Top = req.Top }, ct)), ct);
}

public sealed class CreateMeetingTableRoute : CreateMeetingTableRequest { public Guid HallId { get; set; } }

public sealed class CreateMeetingTableEndpoint(IBusinessMeetingService service)
    : Endpoint<CreateMeetingTableRoute, ApiResult<MeetingTableRow>>
{
    public override void Configure()
    {
        Post("/admin/halls/{hallId:guid}/meeting-tables");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.MeetingTables.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(CreateMeetingTableRoute req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<MeetingTableRow>.Ok(
            await service.CreateTableAsync(actorId, req.HallId,
                new CreateMeetingTableRequest
                {
                    Code = req.Code,
                    RowLabel = req.RowLabel,
                    ColumnNumber = req.ColumnNumber,
                    Capacity = req.Capacity,
                }, ct)), ct);
    }
}

public sealed class UpdateMeetingTableRoute : UpdateMeetingTableRequest { public Guid Id { get; set; } }

public sealed class UpdateMeetingTableEndpoint(IBusinessMeetingService service)
    : Endpoint<UpdateMeetingTableRoute, ApiResult<MeetingTableRow>>
{
    public override void Configure()
    {
        Put("/admin/meeting-tables/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.MeetingTables.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(UpdateMeetingTableRoute req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<MeetingTableRow>.Ok(
            await service.UpdateTableAsync(actorId, req.Id,
                new UpdateMeetingTableRequest
                {
                    Code = req.Code,
                    RowLabel = req.RowLabel,
                    ColumnNumber = req.ColumnNumber,
                    Capacity = req.Capacity,
                }, ct)), ct);
    }
}

public sealed class DeleteMeetingTableRequest { public Guid Id { get; set; } }

public sealed class DeleteMeetingTableEndpoint(IBusinessMeetingService service)
    : Endpoint<DeleteMeetingTableRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/meeting-tables/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.MeetingTables.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(DeleteMeetingTableRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await service.DeleteTableAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

public sealed class GenerateMeetingTablesRoute : GenerateMeetingTablesRequest { public Guid HallId { get; set; } }

public sealed class GenerateMeetingTablesEndpoint(IBusinessMeetingService service)
    : Endpoint<GenerateMeetingTablesRoute, ApiResult<MeetingTablesGenerated>>
{
    public override void Configure()
    {
        Post("/admin/halls/{hallId:guid}/meeting-tables/generate");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.MeetingTables.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(GenerateMeetingTablesRoute req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<MeetingTablesGenerated>.Ok(
            await service.GenerateTablesAsync(actorId, req.HallId,
                new GenerateMeetingTablesRequest
                {
                    Mode = req.Mode,
                    Count = req.Count,
                    RowColumnSpec = req.RowColumnSpec,
                    Capacity = req.Capacity,
                    Reset = req.Reset,
                }, ct)), ct);
    }
}

// ── Hall allocations ─────────────────────────────────────────────────────────

public sealed class ListHallAllocationsRequest
{
    public Guid HallId { get; set; }
    public int Skip { get; set; }
    public int Top { get; set; } = 50;
}

public sealed class ListHallAllocationsEndpoint(IBusinessMeetingService service)
    : Endpoint<ListHallAllocationsRequest, ApiResult<GridPage<HallAllocationRow>>>
{
    public override void Configure()
    {
        Post("/admin/halls/{hallId:guid}/hall-allocations/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.HallAllocations.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(ListHallAllocationsRequest req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<HallAllocationRow>>.Ok(
            await service.ListAllocationsAsync(req.HallId,
                new GridQuery { Skip = req.Skip, Top = req.Top }, ct)), ct);
}

public sealed class CreateHallAllocationRoute : CreateHallAllocationRequest { public Guid HallId { get; set; } }

public sealed class CreateHallAllocationEndpoint(IBusinessMeetingService service)
    : Endpoint<CreateHallAllocationRoute, ApiResult<HallAllocationRow>>
{
    public override void Configure()
    {
        Post("/admin/halls/{hallId:guid}/hall-allocations");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.HallAllocations.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(CreateHallAllocationRoute req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<HallAllocationRow>.Ok(
            await service.CreateAllocationAsync(actorId, req.HallId,
                new CreateHallAllocationRequest
                {
                    Purpose = req.Purpose,
                    Mode = req.Mode,
                    UnitCount = req.UnitCount,
                    RowColumnSpec = req.RowColumnSpec,
                    Start = req.Start,
                    End = req.End,
                    Notes = req.Notes,
                }, ct)), ct);
    }
}

public sealed class ReleaseHallAllocationRequest { public Guid Id { get; set; } }

public sealed class ReleaseHallAllocationEndpoint(IBusinessMeetingService service)
    : Endpoint<ReleaseHallAllocationRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/hall-allocations/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.HallAllocations.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(ReleaseHallAllocationRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await service.ReleaseAllocationAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

// ── Business meetings ────────────────────────────────────────────────────────

public sealed class ListBusinessMeetingsEndpoint(IBusinessMeetingService service)
    : Endpoint<GridQuery, ApiResult<GridPage<BusinessMeetingRow>>>
{
    public override void Configure()
    {
        Post("/admin/business-meetings/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.BusinessMeetings.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<BusinessMeetingRow>>.Ok(
            await service.ListMeetingsAsync(req, ct)), ct);
}

public sealed class GetBusinessMeetingRequest { public Guid Id { get; set; } }

public sealed class GetBusinessMeetingEndpoint(IBusinessMeetingService service)
    : Endpoint<GetBusinessMeetingRequest, ApiResult<BusinessMeetingDetail>>
{
    public override void Configure()
    {
        Get("/admin/business-meetings/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.BusinessMeetings.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GetBusinessMeetingRequest req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<BusinessMeetingDetail>.Ok(
            await service.GetMeetingAsync(req.Id, ct)), ct);
}

public sealed class ScheduleBusinessMeetingEndpoint(IBusinessMeetingService service)
    : Endpoint<ScheduleMeetingRequest, ApiResult<BusinessMeetingScheduled>>
{
    public override void Configure()
    {
        Post("/admin/business-meetings");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.BusinessMeetings.Schedule),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(ScheduleMeetingRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<BusinessMeetingScheduled>.Ok(
            await service.ScheduleMeetingAsync(actorId, req, ct)), ct);
    }
}

public sealed class CancelBusinessMeetingRoute : CancelMeetingRequest { public Guid Id { get; set; } }

public sealed class CancelBusinessMeetingEndpoint(IBusinessMeetingService service)
    : Endpoint<CancelBusinessMeetingRoute, ApiResult<bool>>
{
    public override void Configure()
    {
        Post("/admin/business-meetings/{id:guid}/cancel");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.BusinessMeetings.Cancel),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(CancelBusinessMeetingRoute req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await service.CancelMeetingAsync(actorId, req.Id,
            new CancelMeetingRequest { Reason = req.Reason }, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}
