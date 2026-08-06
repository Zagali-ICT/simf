// Tests: SIMF.Api.Tests/AdminSessionsTests.cs
// Tests: SIMF.Api.Tests/SessionLifecycleTests.cs
// Tests: SIMF.Api.Tests/SessionLiveNoticeTests.cs (informational live notice)
// Tests: SIMF.Api.Tests/ArrivalGraceResolutionTests.cs
using FastEndpoints;
using SIMF.Api.RequestContext;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>Admin CRUD over <c>Sessions</c>. Mirrors the SpeakerEndpoints shape;
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
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<AdminSessionDetail>.Ok(
            await service.CreateAsync(actorId, req, ct)), ct);
    }
}

/// <summary>Binds {id} + body via a derived route that INHERITS the
/// contract (see <c>UpdateHallRoute</c>). It used to re-declare the
/// contract's fields and the endpoint hand-copied them across, which is how
/// sessions, gates, profile types and the four routes before them each
/// silently dropped a field on PUT. Passing the bound request straight
/// through makes that drop impossible.</summary>
public sealed class UpdateSessionRequest : AdminUpdateSessionRequest
{
    public Guid Id { get; set; }
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
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<AdminSessionDetail>.Ok(
            await service.UpdateAsync(actorId, req.Id,
                req, ct)), ct);
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
        var actorId = User.ActorId();
        await service.DeactivateAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

/// <summary>The Scientific
/// Committee moves the session along its broadcast lifecycle. Gated by the
/// dedicated <c>Sessions.Publish</c> permission (distinct from edit) so the
/// Committee role can publish without full session-edit rights.</summary>
public sealed class SetSessionStatusEndpoint(IAdminSessionService service)
    : Endpoint<SetSessionStatusRequest, ApiResult<AdminSessionDetail>>
{
    public override void Configure()
    {
        Put("/admin/sessions/{id:guid}/status");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Sessions.Publish),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(SetSessionStatusRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        var id = Route<Guid>("id");
        await Send.OkAsync(ApiResult<AdminSessionDetail>.Ok(
            await service.SetStatusAsync(actorId, id, req.Status, ct)), ct);
    }
}
