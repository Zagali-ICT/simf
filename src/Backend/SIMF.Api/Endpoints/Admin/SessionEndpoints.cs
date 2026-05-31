// Tests: SIMF.Api.Tests/AdminSessionsTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>D-165 (gap doc G3) — admin CRUD over <c>Sessions</c>
/// (SIMF-FDS-004 §5.3 + PDF §2.9). Mirrors the SpeakerEndpoints shape;
/// speaker + theme selections ride on the body as M-to-M sets.</summary>
public sealed class ListSessionsEndpoint(IAdminSessionService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminSessionSummary>>>
{
    public override void Configure()
    {
        Post("/admin/sessions/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Sessions.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminSessionSummary>>.Ok(
            await service.ListAllAsync(req, ct)), ct);
}

public sealed class GetSessionRequest { public Guid Id { get; set; } }

public sealed class GetSessionEndpoint(IAdminSessionService service)
    : Endpoint<GetSessionRequest, ApiResult<AdminSessionDetail>>
{
    public override void Configure()
    {
        Get("/admin/sessions/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Sessions.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GetSessionRequest req, CancellationToken ct)
    {
        var detail = await service.GetAsync(req.Id, ct)
            ?? throw new ApiException(
                ErrorCodes.SessionNotFound, 404,
                "The session was not found.",
                "لم يتم العثور على الجلسة.");
        await Send.OkAsync(ApiResult<AdminSessionDetail>.Ok(detail), ct);
    }
}

public sealed class CreateSessionEndpoint(IAdminSessionService service)
    : Endpoint<AdminCreateSessionRequest, ApiResult<AdminSessionDetail>>
{
    public override void Configure()
    {
        Post("/admin/sessions");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Sessions.Create),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(AdminCreateSessionRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminSessionDetail>.Ok(
            await service.CreateAsync(actorId, req, ct)), ct);
    }
}

public sealed class UpdateSessionRequest
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionArabic { get; set; }
    public Guid HallId { get; set; }
    public DateTimeOffset StartUtc { get; set; }
    public DateTimeOffset EndUtc { get; set; }
    public int? CapacityOverride { get; set; }
    public IList<AdminSessionSpeakerEntry> Speakers { get; set; }
        = new List<AdminSessionSpeakerEntry>();
    public IList<Guid> ThemeIds { get; set; } = new List<Guid>();
    public bool IsActive { get; set; } = true;
}

public sealed class UpdateSessionEndpoint(IAdminSessionService service)
    : Endpoint<UpdateSessionRequest, ApiResult<AdminSessionDetail>>
{
    public override void Configure()
    {
        Put("/admin/sessions/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Sessions.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(UpdateSessionRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminSessionDetail>.Ok(
            await service.UpdateAsync(actorId, req.Id,
                new AdminUpdateSessionRequest
                {
                    Code = req.Code,
                    Title = req.Title,
                    TitleArabic = req.TitleArabic,
                    Description = req.Description,
                    DescriptionArabic = req.DescriptionArabic,
                    HallId = req.HallId,
                    StartUtc = req.StartUtc,
                    EndUtc = req.EndUtc,
                    CapacityOverride = req.CapacityOverride,
                    Speakers = req.Speakers,
                    ThemeIds = req.ThemeIds,
                    IsActive = req.IsActive,
                }, ct)), ct);
    }
}

public sealed class DeactivateSessionRequest { public Guid Id { get; set; } }

public sealed class DeactivateSessionEndpoint(IAdminSessionService service)
    : Endpoint<DeactivateSessionRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/sessions/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Sessions.Delete),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(DeactivateSessionRequest req, CancellationToken ct)
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
