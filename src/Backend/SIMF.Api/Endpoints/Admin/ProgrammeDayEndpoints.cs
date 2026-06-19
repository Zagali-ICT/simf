// Tests: SIMF.Api.Tests/ProgrammeDaysTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

// D-452 — admin CRUD over the ProgrammeDay rows (date + bilingual title; the
// logo rides the generic asset endpoints, AssetCategory.ProgrammeDayImage).
// Mirrors SessionCategoryEndpoints: gate + bind + delegate; validation lives in
// the service. The PUT reads the route GUID via Route<Guid>("id").
public sealed class ProgrammeDayByIdRequest
{
    public Guid Id { get; set; }
}

public sealed class ListProgrammeDaysEndpoint(IAdminProgrammeDayService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminProgrammeDaySummary>>>
{
    public override void Configure()
    {
        Post("/admin/programme-days/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.ProgrammeDays.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminProgrammeDaySummary>>.Ok(
            await service.ListAsync(req, ct)), ct);
}

public sealed class GetProgrammeDayEndpoint(IAdminProgrammeDayService service)
    : Endpoint<ProgrammeDayByIdRequest, ApiResult<AdminProgrammeDayDetail>>
{
    public override void Configure()
    {
        Get("/admin/programme-days/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.ProgrammeDays.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(ProgrammeDayByIdRequest req, CancellationToken ct)
    {
        var detail = await service.GetAsync(req.Id, ct)
            ?? throw new ApiException(
                ErrorCodes.ProgrammeDayNotFound, 404,
                "The programme day was not found.",
                "لم يتم العثور على يوم البرنامج.");
        await Send.OkAsync(ApiResult<AdminProgrammeDayDetail>.Ok(detail), ct);
    }
}

public sealed class CreateProgrammeDayEndpoint(IAdminProgrammeDayService service)
    : Endpoint<AdminCreateProgrammeDayRequest, ApiResult<AdminProgrammeDayDetail>>
{
    public override void Configure()
    {
        Post("/admin/programme-days");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.ProgrammeDays.Create),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(AdminCreateProgrammeDayRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminProgrammeDayDetail>.Ok(
            await service.CreateAsync(actorId, req, ct)), ct);
    }
}

public sealed class UpdateProgrammeDayEndpoint(IAdminProgrammeDayService service)
    : Endpoint<AdminUpdateProgrammeDayRequest, ApiResult<AdminProgrammeDayDetail>>
{
    public override void Configure()
    {
        Put("/admin/programme-days/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.ProgrammeDays.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(AdminUpdateProgrammeDayRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminProgrammeDayDetail>.Ok(
            await service.UpdateAsync(actorId, Route<Guid>("id"), req, ct)), ct);
    }
}

public sealed class DeactivateProgrammeDayEndpoint(IAdminProgrammeDayService service)
    : Endpoint<ProgrammeDayByIdRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/programme-days/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.ProgrammeDays.Delete),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(ProgrammeDayByIdRequest req, CancellationToken ct)
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
