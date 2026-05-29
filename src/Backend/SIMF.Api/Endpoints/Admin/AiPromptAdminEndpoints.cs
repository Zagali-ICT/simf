// Tests: SIMF.Api.Tests/AiAdminTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.Ai.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Ai;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>D-176 (gap doc G12) — admin CRUD + dry-run + invocations
/// log over the AI prompt catalogue.</summary>
public sealed class ListAiPromptsEndpoint(IAdminAiPromptService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminAiPromptSummary>>>
{
    public override void Configure()
    {
        Post("/admin/ai/prompts/list");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
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
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
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

public sealed class CreateAiPromptEndpoint(IAdminAiPromptService service)
    : Endpoint<CreateAiPromptRequest, ApiResult<AdminAiPromptDetail>>
{
    public override void Configure()
    {
        Post("/admin/ai/prompts");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(CreateAiPromptRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
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
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(UpdateAiPromptRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
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
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(DeleteAiPromptRoute req, CancellationToken ct)
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

public sealed class TestAiPromptRoute : TestAiPromptRequest { public Guid Id { get; set; } }

public sealed class TestAiPromptEndpoint(IAdminAiPromptService service)
    : Endpoint<TestAiPromptRoute, ApiResult<AiCallResult>>
{
    public override void Configure()
    {
        Post("/admin/ai/prompts/{id:guid}/test");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(TestAiPromptRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
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
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminAiInvocationRow>>.Ok(
            await service.ListInvocationsAsync(req, ct)), ct);
}
