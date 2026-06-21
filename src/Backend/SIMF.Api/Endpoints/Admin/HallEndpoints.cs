// Tests: SIMF.Api.Tests/AdminHallsTests.cs
using System.Security.Claims;
using FastEndpoints;
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
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminHallDetail>.Ok(
            await service.CreateAsync(actorId, req, ct)), ct);
    }
}

public sealed class UpdateHallRequest
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string? Floor { get; set; }
    public string? EquipmentNotes { get; set; }
    public bool IsActive { get; set; } = true;
    // D-485: 0 = AssignedSeat, 1 = OpenSeating.
    public int SeatSelectionMode { get; set; }
}

public sealed class UpdateHallEndpoint(IAdminHallService service)
    : Endpoint<UpdateHallRequest, ApiResult<AdminHallDetail>>
{
    public override void Configure()
    {
        Put("/admin/halls/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Halls.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(UpdateHallRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminHallDetail>.Ok(
            await service.UpdateAsync(actorId, req.Id,
                new AdminUpdateHallRequest
                {
                    Code = req.Code, Name = req.Name, NameArabic = req.NameArabic,
                    Capacity = req.Capacity, Floor = req.Floor,
                    EquipmentNotes = req.EquipmentNotes, IsActive = req.IsActive,
                    SeatSelectionMode = req.SeatSelectionMode,
                }, ct)), ct);
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
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await service.DeactivateAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}
