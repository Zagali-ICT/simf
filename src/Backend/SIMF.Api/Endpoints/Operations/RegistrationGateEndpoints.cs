// Tests: SIMF.Api.Tests/RegistrationGateTests.cs
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Api.RequestContext;
using SIMF.Application.Operations.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Operations;

/// <summary>Admin GET + PUT for the
/// registration gate. Reading is admin-only (the public sign-up gates on
/// the service-side check, no public endpoint needed yet).</summary>
public sealed class GetRegistrationGateEndpoint(IOperationsToggleService service)
    : EndpointWithoutRequest<ApiResult<RegistrationGateState>>
{
    public override void Configure()
    {
        Get("/admin/registration-gate");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Operations.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<RegistrationGateState>.Ok(
            await service.GetRegistrationGateAsync(ct)), ct);
}

public sealed class UpdateRegistrationGateEndpoint(IOperationsToggleService service)
    : Endpoint<UpdateRegistrationGateRequest, ApiResult<RegistrationGateState>>
{
    public override void Configure()
    {
        Put("/admin/registration-gate");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Operations.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(
        UpdateRegistrationGateRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await Send.OkAsync(ApiResult<RegistrationGateState>.Ok(
            await service.UpdateRegistrationGateAsync(actorId, req, ct)), ct);
    }
}
