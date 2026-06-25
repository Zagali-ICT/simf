// Tests: SIMF.Api.Tests/RatingConfigTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.Feedback.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Feedback;

namespace SIMF.Api.Endpoints.Admin;

// Admin CRUD over the rating configuration (types → question groups → questions).
// Validation lives in the service (mirrors AdminFaqService); endpoints gate +
// bind + delegate. Route GUIDs are read via Route<Guid>(...) since the request
// records are sealed.

public sealed class RatingByIdRequest { public Guid Id { get; set; } }

// -- Types --

public sealed class ListRatingTypesEndpoint(IAdminRatingConfigService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminRatingTypeSummary>>>
{
    public override void Configure()
    {
        Post("/admin/ratings/types/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.RatingConfig.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminRatingTypeSummary>>.Ok(
            await service.ListTypesAsync(req, ct)), ct);
}

public sealed class GetRatingTypeEndpoint(IAdminRatingConfigService service)
    : Endpoint<RatingByIdRequest, ApiResult<AdminRatingTypeSummary>>
{
    public override void Configure()
    {
        Get("/admin/ratings/types/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.RatingConfig.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(RatingByIdRequest req, CancellationToken ct)
    {
        var type = await service.GetTypeAsync(req.Id, ct)
            ?? throw new ApiException(ErrorCodes.RatingTypeNotFound, 404,
                "The rating type was not found.", "لم يتم العثور على نوع التقييم.");
        await Send.OkAsync(ApiResult<AdminRatingTypeSummary>.Ok(type), ct);
    }
}

public sealed class CreateRatingTypeEndpoint(IAdminRatingConfigService service)
    : Endpoint<CreateRatingTypeRequest, ApiResult<AdminRatingTypeSummary>>
{
    public override void Configure()
    {
        Post("/admin/ratings/types");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.RatingConfig.Create), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(CreateRatingTypeRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminRatingTypeSummary>.Ok(
            await service.CreateTypeAsync(actorId, req, ct)), ct);
    }
}

public sealed class UpdateRatingTypeEndpoint(IAdminRatingConfigService service)
    : Endpoint<UpdateRatingTypeRequest, ApiResult<AdminRatingTypeSummary>>
{
    public override void Configure()
    {
        Put("/admin/ratings/types/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.RatingConfig.Edit), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(UpdateRatingTypeRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminRatingTypeSummary>.Ok(
            await service.UpdateTypeAsync(actorId, Route<Guid>("id"), req, ct)), ct);
    }
}

public sealed class DeleteRatingTypeEndpoint(IAdminRatingConfigService service)
    : Endpoint<RatingByIdRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/ratings/types/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.RatingConfig.Delete), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(RatingByIdRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await service.DeactivateTypeAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

// -- Question groups --

public sealed class ListRatingGroupsEndpoint(IAdminRatingConfigService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminRatingQuestionGroupSummary>>>
{
    public override void Configure()
    {
        Post("/admin/ratings/types/{typeId:guid}/groups/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.RatingConfig.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminRatingQuestionGroupSummary>>.Ok(
            await service.ListGroupsAsync(Route<Guid>("typeId"), req, ct)), ct);
}

public sealed class GetRatingGroupEndpoint(IAdminRatingConfigService service)
    : Endpoint<RatingByIdRequest, ApiResult<AdminRatingQuestionGroupSummary>>
{
    public override void Configure()
    {
        Get("/admin/ratings/groups/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.RatingConfig.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(RatingByIdRequest req, CancellationToken ct)
    {
        var group = await service.GetGroupAsync(req.Id, ct)
            ?? throw new ApiException(ErrorCodes.RatingGroupNotFound, 404,
                "The rating question group was not found.", "لم يتم العثور على مجموعة الأسئلة.");
        await Send.OkAsync(ApiResult<AdminRatingQuestionGroupSummary>.Ok(group), ct);
    }
}

public sealed class CreateRatingGroupEndpoint(IAdminRatingConfigService service)
    : Endpoint<CreateRatingQuestionGroupRequest, ApiResult<AdminRatingQuestionGroupSummary>>
{
    public override void Configure()
    {
        Post("/admin/ratings/groups");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.RatingConfig.Create), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(CreateRatingQuestionGroupRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminRatingQuestionGroupSummary>.Ok(
            await service.CreateGroupAsync(actorId, req, ct)), ct);
    }
}

public sealed class UpdateRatingGroupEndpoint(IAdminRatingConfigService service)
    : Endpoint<UpdateRatingQuestionGroupRequest, ApiResult<AdminRatingQuestionGroupSummary>>
{
    public override void Configure()
    {
        Put("/admin/ratings/groups/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.RatingConfig.Edit), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(UpdateRatingQuestionGroupRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminRatingQuestionGroupSummary>.Ok(
            await service.UpdateGroupAsync(actorId, Route<Guid>("id"), req, ct)), ct);
    }
}

public sealed class DeleteRatingGroupEndpoint(IAdminRatingConfigService service)
    : Endpoint<RatingByIdRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/ratings/groups/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.RatingConfig.Delete), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(RatingByIdRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await service.DeactivateGroupAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

// -- Questions --

public sealed class ListRatingQuestionsEndpoint(IAdminRatingConfigService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminRatingQuestionSummary>>>
{
    public override void Configure()
    {
        Post("/admin/ratings/types/{typeId:guid}/questions/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.RatingConfig.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminRatingQuestionSummary>>.Ok(
            await service.ListQuestionsAsync(Route<Guid>("typeId"), req, ct)), ct);
}

public sealed class GetRatingQuestionEndpoint(IAdminRatingConfigService service)
    : Endpoint<RatingByIdRequest, ApiResult<AdminRatingQuestionSummary>>
{
    public override void Configure()
    {
        Get("/admin/ratings/questions/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.RatingConfig.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(RatingByIdRequest req, CancellationToken ct)
    {
        var question = await service.GetQuestionAsync(req.Id, ct)
            ?? throw new ApiException(ErrorCodes.RatingQuestionNotFound, 404,
                "The rating question was not found.", "لم يتم العثور على السؤال.");
        await Send.OkAsync(ApiResult<AdminRatingQuestionSummary>.Ok(question), ct);
    }
}

public sealed class CreateRatingQuestionEndpoint(IAdminRatingConfigService service)
    : Endpoint<CreateRatingQuestionRequest, ApiResult<AdminRatingQuestionSummary>>
{
    public override void Configure()
    {
        Post("/admin/ratings/questions");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.RatingConfig.Create), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(CreateRatingQuestionRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminRatingQuestionSummary>.Ok(
            await service.CreateQuestionAsync(actorId, req, ct)), ct);
    }
}

public sealed class UpdateRatingQuestionEndpoint(IAdminRatingConfigService service)
    : Endpoint<UpdateRatingQuestionRequest, ApiResult<AdminRatingQuestionSummary>>
{
    public override void Configure()
    {
        Put("/admin/ratings/questions/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.RatingConfig.Edit), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(UpdateRatingQuestionRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminRatingQuestionSummary>.Ok(
            await service.UpdateQuestionAsync(actorId, Route<Guid>("id"), req, ct)), ct);
    }
}

public sealed class DeleteRatingQuestionEndpoint(IAdminRatingConfigService service)
    : Endpoint<RatingByIdRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/ratings/questions/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.RatingConfig.Delete), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(RatingByIdRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await service.DeactivateQuestionAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}
