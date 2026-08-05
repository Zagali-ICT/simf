// Tests: SIMF.Api.Tests/FaqTests.cs
using FastEndpoints;
using SIMF.Api.RequestContext;
using SIMF.Application.Faq.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Faq;

namespace SIMF.Api.Endpoints.Admin;

// P2.1 (D-211) — admin CRUD over the two-level FAQ. Validation lives in the
// service (mirrors AdminNewsService); endpoints just gate + bind + delegate.

// GridQuery + the contract request records are sealed, so route GUIDs are read
// via Route<Guid>(...) rather than a derived route-merge type. GET/DELETE bind
// the {id} route to this small DTO.
public sealed class FaqByIdRequest { public Guid Id { get; set; } }

// -- Groups --

public sealed class ListFaqGroupsEndpoint(IAdminFaqService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminFaqGroupSummary>>>
{
    public override void Configure()
    {
        Post("/admin/faq/groups/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Faq.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminFaqGroupSummary>>.Ok(
            await service.ListGroupsAsync(req, ct)), ct);
}

public sealed class GetFaqGroupEndpoint(IAdminFaqService service)
    : Endpoint<FaqByIdRequest, ApiResult<AdminFaqGroupSummary>>
{
    public override void Configure()
    {
        Get("/admin/faq/groups/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Faq.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(FaqByIdRequest req, CancellationToken ct)
    {
        var group = await service.GetGroupAsync(req.Id, ct)
            ?? throw new ApiException(ErrorCodes.FaqGroupNotFound, 404,
                "The FAQ group was not found.", "لم يتم العثور على مجموعة الأسئلة.");
        await Send.OkAsync(ApiResult<AdminFaqGroupSummary>.Ok(group), ct);
    }
}

public sealed class CreateFaqGroupEndpoint(IAdminFaqService service)
    : Endpoint<CreateFaqGroupRequest, ApiResult<AdminFaqGroupSummary>>
{
    public override void Configure()
    {
        Post("/admin/faq/groups");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Faq.Create), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(CreateFaqGroupRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<AdminFaqGroupSummary>.Ok(
            await service.CreateGroupAsync(actorId, req, ct)), ct);
    }
}

public sealed class UpdateFaqGroupEndpoint(IAdminFaqService service)
    : Endpoint<UpdateFaqGroupRequest, ApiResult<AdminFaqGroupSummary>>
{
    public override void Configure()
    {
        Put("/admin/faq/groups/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Faq.Edit), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(UpdateFaqGroupRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<AdminFaqGroupSummary>.Ok(
            await service.UpdateGroupAsync(actorId, Route<Guid>("id"), req, ct)), ct);
    }
}

public sealed class DeleteFaqGroupEndpoint(IAdminFaqService service)
    : Endpoint<FaqByIdRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/faq/groups/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Faq.Delete), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(FaqByIdRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await service.DeactivateGroupAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

// -- Entries --

public sealed class ListFaqEntriesEndpoint(IAdminFaqService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminFaqEntrySummary>>>
{
    public override void Configure()
    {
        Post("/admin/faq/groups/{groupId:guid}/entries/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Faq.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminFaqEntrySummary>>.Ok(
            await service.ListEntriesAsync(Route<Guid>("groupId"), req, ct)), ct);
}

public sealed class GetFaqEntryEndpoint(IAdminFaqService service)
    : Endpoint<FaqByIdRequest, ApiResult<AdminFaqEntrySummary>>
{
    public override void Configure()
    {
        Get("/admin/faq/entries/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Faq.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(FaqByIdRequest req, CancellationToken ct)
    {
        var entry = await service.GetEntryAsync(req.Id, ct)
            ?? throw new ApiException(ErrorCodes.FaqEntryNotFound, 404,
                "The FAQ entry was not found.", "لم يتم العثور على السؤال.");
        await Send.OkAsync(ApiResult<AdminFaqEntrySummary>.Ok(entry), ct);
    }
}

public sealed class CreateFaqEntryEndpoint(IAdminFaqService service)
    : Endpoint<CreateFaqEntryRequest, ApiResult<AdminFaqEntrySummary>>
{
    public override void Configure()
    {
        Post("/admin/faq/entries");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Faq.Create), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(CreateFaqEntryRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<AdminFaqEntrySummary>.Ok(
            await service.CreateEntryAsync(actorId, req, ct)), ct);
    }
}

public sealed class UpdateFaqEntryEndpoint(IAdminFaqService service)
    : Endpoint<UpdateFaqEntryRequest, ApiResult<AdminFaqEntrySummary>>
{
    public override void Configure()
    {
        Put("/admin/faq/entries/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Faq.Edit), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(UpdateFaqEntryRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<AdminFaqEntrySummary>.Ok(
            await service.UpdateEntryAsync(actorId, Route<Guid>("id"), req, ct)), ct);
    }
}

public sealed class DeleteFaqEntryEndpoint(IAdminFaqService service)
    : Endpoint<FaqByIdRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/faq/entries/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Faq.Delete), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(FaqByIdRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await service.DeactivateEntryAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}
