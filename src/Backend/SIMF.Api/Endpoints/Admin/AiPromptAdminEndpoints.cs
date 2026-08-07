// Tests: SIMF.Api.Tests/AiAdminTests.cs, SIMF.Api.Tests/AiHardeningTests.cs
using System.Text.Json;
using FastEndpoints;
using SIMF.Api.RequestContext;
using SIMF.Application.Ai.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Ai;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>Admin CRUD + dry-run + invocations
/// log over the AI prompt catalogue.</summary>
public sealed class ListAiPromptsEndpoint(IAdminAiPromptService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminAiPromptSummary>>>
{
    public override void Configure()
    {
        Post("/admin/ai/prompts/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.AiPrompts.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminAiPromptSummary>>.Ok(
            await service.ListAsync(req, ct)), ct);
}

public sealed class GetAiPromptRoute { public Guid Id { get; set; } }

public sealed class GetAiPromptEndpoint(IAdminAiPromptService service)
    : Endpoint<GetAiPromptRoute, ApiResult<AdminAiPromptDetail>>
{
    public override void Configure()
    {
        Get("/admin/ai/prompts/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.AiPrompts.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(GetAiPromptRoute req, CancellationToken ct)
    {
        var detail = await service.GetAsync(req.Id, ct)
            ?? throw new ApiException(
                ErrorCodes.AiPromptNotFound, 404,
                "AI prompt not found.",
                "لم يتم العثور على محفّز الذكاء الاصطناعي.");
        await Send.OkAsync(ApiResult<AdminAiPromptDetail>.Ok(detail), ct);
    }
}

/// <summary><c>GET /admin/ai/prompts/{id}/history</c> returns
/// the append-only snapshot list (newest first) for the given prompt.
/// Empty list when the prompt has never been updated.</summary>
public sealed class GetAiPromptHistoryEndpoint(IAdminAiPromptService service)
    : Endpoint<GetAiPromptRoute, ApiResult<IReadOnlyList<AdminAiPromptHistoryEntry>>>
{
    public override void Configure()
    {
        Get("/admin/ai/prompts/{id:guid}/history");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.AiPrompts.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        // Per-record drill-down on the prompt-edit history is
        // an audit-content read — apply the same auth rate-limit as
        // the meeting-request detail endpoint, so
        // a compromised admin cannot burst-scrape historical
        // prompt-text without hitting the limiter.
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(GetAiPromptRoute req, CancellationToken ct)
    {
        var history = await service.GetHistoryAsync(req.Id, ct);
        await Send.OkAsync(ApiResult<IReadOnlyList<AdminAiPromptHistoryEntry>>.Ok(history), ct);
    }
}

public sealed class CreateAiPromptEndpoint(IAdminAiPromptService service)
    : Endpoint<CreateAiPromptRequest, ApiResult<AdminAiPromptDetail>>
{
    public override void Configure()
    {
        Post("/admin/ai/prompts");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.AiPrompts.Create),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(CreateAiPromptRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<AdminAiPromptDetail>.Ok(
            await service.CreateAsync(actorId, req, ct)), ct);
    }
}

public sealed class UpdateAiPromptRoute : UpdateAiPromptRequest { public Guid Id { get; set; } }

public sealed class UpdateAiPromptEndpoint(IAdminAiPromptService service)
    : Endpoint<UpdateAiPromptRoute, ApiResult<AdminAiPromptDetail>>
{
    public override void Configure()
    {
        Put("/admin/ai/prompts/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.AiPrompts.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(UpdateAiPromptRoute req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<AdminAiPromptDetail>.Ok(
            await service.UpdateAsync(actorId, req.Id, req, ct)), ct);
    }
}

public sealed class DeleteAiPromptRoute { public Guid Id { get; set; } }

public sealed class DeleteAiPromptEndpoint(IAdminAiPromptService service)
    : Endpoint<DeleteAiPromptRoute, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/ai/prompts/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.AiPrompts.Delete),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(DeleteAiPromptRoute req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await service.DeactivateAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

public sealed class TestAiPromptRoute : TestAiPromptRequest { public Guid Id { get; set; } }

public sealed class TestAiPromptEndpoint(IAdminAiPromptService service)
    : Endpoint<TestAiPromptRoute, ApiResult<AiCallResult>>
{
    public override void Configure()
    {
        Post("/admin/ai/prompts/{id:guid}/test");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.AiPrompts.Test),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        // Per-admin partition (sub-keyed) on the dry-run
        // endpoint, not just per-IP "auth", so shared offices and
        // stolen-credential botnets cannot bypass the cap.
        Options(rb => rb.RequireRateLimiting("ai-test"));
        Tags("Admin");
    }
    public override async Task HandleAsync(TestAiPromptRoute req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<AiCallResult>.Ok(
            await service.TestAsync(actorId, req.Id, req, ct)), ct);
    }
}

public sealed class ListAiInvocationsEndpoint(IAdminAiPromptService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminAiInvocationRow>>>
{
    public override void Configure()
    {
        Post("/admin/ai/invocations/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.AiInvocations.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminAiInvocationRow>>.Ok(
            await service.ListInvocationsAsync(req, ct)), ct);
}

/// <summary>CP Phase-1 — the AI Control Center dashboard: rolled-up invocation
/// health (calls / errors / latency / tokens, overall + per service) over the
/// last 24h plus the configured-service counts. Read-only; its own permission
/// (<c>AiDashboard.View</c>) gates both this endpoint and the CP page.</summary>
public sealed class GetAiDashboardEndpoint(IAdminAiPromptService service)
    : EndpointWithoutRequest<ApiResult<AdminAiDashboard>>
{
    public override void Configure()
    {
        Get("/admin/ai/dashboard");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.AiDashboard.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<AdminAiDashboard>.Ok(
            await service.GetDashboardAsync(24, ct)), ct);
}

/// <summary>SOC drill-down: returns the full invocation
/// payload (<c>InputJson</c> + <c>OutputText</c>) for one row. The
/// grid list deliberately omits these for lightness + PII discipline;
/// this endpoint is the audit-trail read for threat hunting (e.g.
/// search the input column for jailbreak phrases). Inputs are already
/// redacted at write time by <c>AiAuditDetail.SerialiseAndRedact</c>,
/// so SOC sees masked PII/keys; output text is the LLM response
/// verbatim and should be treated as restricted. Admin-only for now;
/// when a dedicated SecurityAuditor role lands, swap the policy.</summary>
public sealed class GetAiInvocationDetailRoute { public Guid Id { get; set; } }

public sealed class GetAiInvocationDetailEndpoint(
    IAdminAiPromptService service,
    IAuditLog auditLog)
    : Endpoint<GetAiInvocationDetailRoute, ApiResult<AdminAiInvocationDetail>>
{
    public override void Configure()
    {
        Get("/admin/ai/invocations/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.AiInvocations.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        // Same per-admin window as the Test
        // endpoint so an admin can't pull the whole invocation log
        // one row at a time uncapped.
        Options(rb => rb.RequireRateLimiting("ai-test"));
        Tags("Admin");
    }
    public override async Task HandleAsync(
        GetAiInvocationDetailRoute req, CancellationToken ct)
    {
        var detail = await service.GetInvocationAsync(req.Id, ct)
            ?? throw new ApiException(
                ErrorCodes.NotFound, 404,
                "AI invocation not found.",
                "لم يتم العثور على استدعاء الذكاء الاصطناعي.");

        // Admin-on-admin surveillance trail.
        // Without this, "admin reads 50k invocations on Sunday night"
        // is invisible to SOC.
        Guid? viewedBy = User.ActorIdOrNull();
        await auditLog.WriteSuccessAsync(
            AuditEvents.AiInvocationViewed,
            viewedBy ?? Guid.Empty,
            JsonSerializer.Serialize(new
            {
                invocationId = detail.Id,
                promptKey = detail.PromptKey,
                feature = detail.Feature.ToString(),
                hasOutput = detail.OutputText is not null,
            }),
            ct);

        await Send.OkAsync(ApiResult<AdminAiInvocationDetail>.Ok(detail), ct);
    }
}
