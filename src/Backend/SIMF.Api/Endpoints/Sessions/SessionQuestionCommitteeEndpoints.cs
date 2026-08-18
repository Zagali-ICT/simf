// Tests: SIMF.Api.Tests/SessionQuestionCommitteeTests.cs
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Api.RequestContext;
using SIMF.Application.SessionQuestions.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Sessions;

namespace SIMF.Api.Endpoints.Sessions;

/// <summary>The Scientific-Committee
/// central Q&amp;A queue (stage 2). Cross-session, gated by the Questions.*
/// permissions — View to read, Moderate to approve/hide, Escalate to route to a
/// role. Distinct from the per-session moderator desk (stage 3).
///
/// <para>Server-paged on the shared grid seam, so it is a POST carrying a
/// <see cref="GridQuery"/> like every other Control Panel list. The status and
/// session that used to be query-string parameters are now grid filter keys
/// (<c>status</c>, <c>sessionId</c>); a request naming no status gets the
/// default Pending bucket.</para></summary>
public sealed class ListQuestionQueueEndpoint(ISessionQuestionCommitteeService service)
    : Endpoint<GridQuery, ApiResult<GridPage<SessionQuestionQueueRow>>>
{
    public override void Configure()
    {
        Post("/admin/questions/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Questions.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<SessionQuestionQueueRow>>.Ok(
            await service.ListQueueAsync(req, ct)), ct);
}

public sealed class ApproveQuestionEndpoint(ISessionQuestionCommitteeService service)
    : EndpointWithoutRequest<ApiResult<SessionQuestionQueueRow>>
{
    public override void Configure()
    {
        Put("/admin/questions/{questionId:guid}/approve");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Questions.Moderate),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var actorId = User.ActorId();
        var id = Route<Guid>("questionId");
        await Send.OkAsync(ApiResult<SessionQuestionQueueRow>.Ok(
            await service.ApproveAsync(actorId, id, ct)), ct);
    }
}

public sealed class HideQuestionFromQueueEndpoint(ISessionQuestionCommitteeService service)
    : EndpointWithoutRequest<ApiResult<SessionQuestionQueueRow>>
{
    public override void Configure()
    {
        Put("/admin/questions/{questionId:guid}/hide");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Questions.Moderate),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var actorId = User.ActorId();
        var id = Route<Guid>("questionId");
        await Send.OkAsync(ApiResult<SessionQuestionQueueRow>.Ok(
            await service.HideAsync(actorId, id, ct)), ct);
    }
}

public sealed class EscalateQuestionEndpoint(ISessionQuestionCommitteeService service)
    : Endpoint<EscalateQuestionRequest, ApiResult<SessionQuestionQueueRow>>
{
    public override void Configure()
    {
        Put("/admin/questions/{questionId:guid}/escalate");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Questions.Escalate),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(EscalateQuestionRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        var id = Route<Guid>("questionId");
        await Send.OkAsync(ApiResult<SessionQuestionQueueRow>.Ok(
            await service.EscalateAsync(actorId, id, req.Role, ct)), ct);
    }
}
