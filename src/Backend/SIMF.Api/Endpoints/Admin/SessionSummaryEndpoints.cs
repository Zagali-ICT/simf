// Tests: SIMF.Api.Tests/SessionSummaryCommitteeTests.cs
using FastEndpoints;
using SIMF.Api.RequestContext;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>The Scientific-Committee session-summary / محضر desk.
/// List + read are gated by
/// <c>SessionSummaries.View</c>, generate + save by <c>.Edit</c>, publish +
/// un-publish by <c>.Publish</c> — all on top of RequireApprovedAccount.
/// <para>The desk list is server-paged on the shared grid seam, so it is a POST
/// carrying a <see cref="GridQuery"/> like every other Control Panel list. The
/// literal <c>list</c> segment cannot collide with the sibling
/// <c>{sessionId:guid}</c> routes.</para></summary>
public sealed class ListSessionSummariesEndpoint(IAdminSessionSummaryService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminSessionSummaryRow>>>
{
    public override void Configure()
    {
        Post("/admin/session-summaries/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SessionSummaries.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminSessionSummaryRow>>.Ok(
            await service.ListAsync(req, ct)), ct);
}

public sealed class GetSessionSummaryAdminEndpoint(IAdminSessionSummaryService service)
    : EndpointWithoutRequest<ApiResult<AdminSessionSummaryDetail>>
{
    public override void Configure()
    {
        Get("/admin/session-summaries/{sessionId:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SessionSummaries.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var sessionId = Route<Guid>("sessionId");
        var summary = await service.GetAsync(sessionId, ct)
            ?? throw new ApiException(
                ErrorCodes.SessionSummaryNotFound, 404,
                "No summary exists for this session yet.",
                "لا يوجد ملخّص لهذه الجلسة بعد.");
        await Send.OkAsync(ApiResult<AdminSessionSummaryDetail>.Ok(summary), ct);
    }
}

public sealed class GenerateSessionSummaryEndpoint(IAdminSessionSummaryService service)
    : EndpointWithoutRequest<ApiResult<AdminSessionSummaryDetail>>
{
    public override void Configure()
    {
        Post("/admin/session-summaries/{sessionId:guid}/generate");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SessionSummaries.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var actorId = User.ActorId();
        var sessionId = Route<Guid>("sessionId");
        await Send.OkAsync(ApiResult<AdminSessionSummaryDetail>.Ok(
            await service.GenerateAsync(actorId, sessionId, ct)), ct);
    }
}

public sealed class SaveSessionSummaryEndpoint(IAdminSessionSummaryService service)
    : Endpoint<SaveSessionSummaryRequest, ApiResult<AdminSessionSummaryDetail>>
{
    public override void Configure()
    {
        Put("/admin/session-summaries/{sessionId:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SessionSummaries.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(SaveSessionSummaryRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        var sessionId = Route<Guid>("sessionId");
        await Send.OkAsync(ApiResult<AdminSessionSummaryDetail>.Ok(
            await service.SaveAsync(actorId, sessionId, req, ct)), ct);
    }
}

public sealed class PublishSessionSummaryEndpoint(IAdminSessionSummaryService service)
    : EndpointWithoutRequest<ApiResult<AdminSessionSummaryDetail>>
{
    public override void Configure()
    {
        Put("/admin/session-summaries/{sessionId:guid}/publish");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SessionSummaries.Publish),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var actorId = User.ActorId();
        var sessionId = Route<Guid>("sessionId");
        await Send.OkAsync(ApiResult<AdminSessionSummaryDetail>.Ok(
            await service.PublishAsync(actorId, sessionId, ct)), ct);
    }
}

public sealed class UnpublishSessionSummaryEndpoint(IAdminSessionSummaryService service)
    : EndpointWithoutRequest<ApiResult<AdminSessionSummaryDetail>>
{
    public override void Configure()
    {
        Put("/admin/session-summaries/{sessionId:guid}/unpublish");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SessionSummaries.Publish),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var actorId = User.ActorId();
        var sessionId = Route<Guid>("sessionId");
        await Send.OkAsync(ApiResult<AdminSessionSummaryDetail>.Ok(
            await service.UnpublishAsync(actorId, sessionId, ct)), ct);
    }
}

// -- The team review/approval workflow ----------------------------------------
// Submit (the editing team, .Edit) → Approve / Return-to-draft (the approving
// team, .Approve). The approved محضر becomes readable by the host/moderator.

public sealed class SubmitSessionSummaryForReviewEndpoint(IAdminSessionSummaryService service)
    : EndpointWithoutRequest<ApiResult<AdminSessionSummaryDetail>>
{
    public override void Configure()
    {
        Put("/admin/session-summaries/{sessionId:guid}/submit-review");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SessionSummaries.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var actorId = User.ActorId();
        var sessionId = Route<Guid>("sessionId");
        await Send.OkAsync(ApiResult<AdminSessionSummaryDetail>.Ok(
            await service.SubmitForReviewAsync(actorId, sessionId, ct)), ct);
    }
}

public sealed class ApproveSessionSummaryEndpoint(IAdminSessionSummaryService service)
    : EndpointWithoutRequest<ApiResult<AdminSessionSummaryDetail>>
{
    public override void Configure()
    {
        Put("/admin/session-summaries/{sessionId:guid}/approve");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SessionSummaries.Approve),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var actorId = User.ActorId();
        var sessionId = Route<Guid>("sessionId");
        await Send.OkAsync(ApiResult<AdminSessionSummaryDetail>.Ok(
            await service.ApproveAsync(actorId, sessionId, ct)), ct);
    }
}

public sealed class ReturnSessionSummaryToDraftEndpoint(IAdminSessionSummaryService service)
    : EndpointWithoutRequest<ApiResult<AdminSessionSummaryDetail>>
{
    public override void Configure()
    {
        Put("/admin/session-summaries/{sessionId:guid}/return-to-draft");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.SessionSummaries.Approve),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var actorId = User.ActorId();
        var sessionId = Route<Guid>("sessionId");
        await Send.OkAsync(ApiResult<AdminSessionSummaryDetail>.Ok(
            await service.ReturnToDraftAsync(actorId, sessionId, ct)), ct);
    }
}
