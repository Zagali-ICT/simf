// Tests: SIMF.Api.Tests/ParticipationDocumentRequestsTests.cs
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Api.RequestContext;
using SIMF.Application.Requests.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Requests;

namespace SIMF.Api.Endpoints.Requests;

/// <summary>D-500 (Wave 5, الطلبات "طلب وثيقة المشاركة") — an approved attendee
/// submits a participation-document request. Login-required.</summary>
public sealed class SubmitParticipationDocumentRequestEndpoint(IParticipationDocumentRequestService service)
    : Endpoint<SubmitParticipationDocumentRequestBody, ApiResult<ParticipationDocumentRequestSubmitted>>
{
    public override void Configure()
    {
        Post("/app/document-requests");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Requests");
    }

    public override async Task HandleAsync(SubmitParticipationDocumentRequestBody req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<ParticipationDocumentRequestSubmitted>.Ok(
            await service.SubmitAsync(actorId, req, ct)), ct);
    }
}

// -- Admin participation-document-request management --

public sealed class ListAdminParticipationDocumentRequestsEndpoint(IParticipationDocumentRequestService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminParticipationDocumentRequestRow>>>
{
    public override void Configure()
    {
        Post("/admin/document-requests/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.ParticipationDocumentRequests.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<GridPage<AdminParticipationDocumentRequestRow>>.Ok(
            await service.ListAllAsync(actorId, req, ct)), ct);
    }
}

public sealed class GetAdminParticipationDocumentRequestRoute
{
    public Guid Id { get; set; }
}

public sealed class GetAdminParticipationDocumentRequestEndpoint(IParticipationDocumentRequestService service)
    : Endpoint<GetAdminParticipationDocumentRequestRoute, ApiResult<AdminParticipationDocumentRequestDetail>>
{
    public override void Configure()
    {
        Get("/admin/document-requests/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.ParticipationDocumentRequests.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(GetAdminParticipationDocumentRequestRoute req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<AdminParticipationDocumentRequestDetail>.Ok(
            await service.GetAsync(actorId, req.Id, ct)), ct);
    }
}

public sealed class RespondToParticipationDocumentRequestRoute : RespondToParticipationDocumentRequestRequest
{
    public Guid Id { get; set; }
}

public sealed class RespondToParticipationDocumentRequestEndpoint(IParticipationDocumentRequestService service)
    : Endpoint<RespondToParticipationDocumentRequestRoute, ApiResult<AdminParticipationDocumentRequestDetail>>
{
    public override void Configure()
    {
        Put("/admin/document-requests/{id:guid}/respond");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.ParticipationDocumentRequests.Manage),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(RespondToParticipationDocumentRequestRoute req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<AdminParticipationDocumentRequestDetail>.Ok(
            await service.RespondAsync(actorId, req.Id, req, ct)), ct);
    }
}
