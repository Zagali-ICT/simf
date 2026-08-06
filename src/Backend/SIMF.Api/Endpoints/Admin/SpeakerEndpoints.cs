// Tests: SIMF.Api.Tests/AdminSpeakersTests.cs
using FastEndpoints;
using SIMF.Api.RequestContext;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>Admin CRUD over <c>Speakers</c>.
/// Mirrors ThemeEndpoints / HallEndpoints / CountryEndpoints shape.</summary>
public sealed class ListSpeakersEndpoint(IAdminSpeakerService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminSpeakerSummary>>>
{
    public override void Configure()
    {
        Post("/admin/speakers/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Speakers.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminSpeakerSummary>>.Ok(
            await service.ListAllAsync(req, ct)), ct);
}

public sealed class GetSpeakerRequest { public Guid Id { get; set; } }

public sealed class GetSpeakerEndpoint(IAdminSpeakerService service)
    : Endpoint<GetSpeakerRequest, ApiResult<AdminSpeakerDetail>>
{
    public override void Configure()
    {
        Get("/admin/speakers/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Speakers.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GetSpeakerRequest req, CancellationToken ct)
    {
        var detail = await service.GetAsync(req.Id, ct)
            ?? throw new ApiException(
                ErrorCodes.SpeakerNotFound, 404,
                "The speaker was not found.",
                "لم يتم العثور على المتحدّث.");
        await Send.OkAsync(ApiResult<AdminSpeakerDetail>.Ok(detail), ct);
    }
}

public sealed class CreateSpeakerEndpoint(IAdminSpeakerService service)
    : Endpoint<AdminCreateSpeakerRequest, ApiResult<AdminSpeakerDetail>>
{
    public override void Configure()
    {
        Post("/admin/speakers");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Speakers.Create),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(AdminCreateSpeakerRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<AdminSpeakerDetail>.Ok(
            await service.CreateAsync(actorId, req, ct)), ct);
    }
}

/// <summary>Binds {id} + body via a derived route that INHERITS the
/// contract (see <c>UpdateHallRoute</c>). It used to re-declare the
/// contract's fields and the endpoint hand-copied them across, which is how
/// the sessions, gates and profile-type routes — and the four before them —
/// each silently dropped a field on PUT. Passing the bound request straight
/// through makes that drop impossible.</summary>
public sealed class UpdateSpeakerRequest : AdminUpdateSpeakerRequest
{
    public Guid Id { get; set; }
}

public sealed class UpdateSpeakerEndpoint(IAdminSpeakerService service)
    : Endpoint<UpdateSpeakerRequest, ApiResult<AdminSpeakerDetail>>
{
    public override void Configure()
    {
        Put("/admin/speakers/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Speakers.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(UpdateSpeakerRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<AdminSpeakerDetail>.Ok(
            await service.UpdateAsync(actorId, req.Id,
                req, ct)), ct);
    }
}

public sealed class DeactivateSpeakerRequest { public Guid Id { get; set; } }

public sealed class DeactivateSpeakerEndpoint(IAdminSpeakerService service)
    : Endpoint<DeactivateSpeakerRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/speakers/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Speakers.Delete),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(DeactivateSpeakerRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await service.DeactivateAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}
