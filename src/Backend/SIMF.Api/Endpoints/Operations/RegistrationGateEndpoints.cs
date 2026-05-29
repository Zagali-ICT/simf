// Tests: SIMF.Api.Tests/RegistrationGateTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.Operations.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Operations;

/// <summary>D-166 (gap doc G4, PDF §2.3) — admin GET + PUT for the
/// registration gate. Reading is admin-only (the public sign-up gates on
/// the service-side check, no public endpoint needed yet).</summary>
public sealed class GetRegistrationGateEndpoint(IOperationsToggleService service)
    : EndpointWithoutRequest<ApiResult<RegistrationGateState>>
{
    public override void Configure()
    {
        Get("/admin/registration-gate");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
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
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(
        UpdateRegistrationGateRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<RegistrationGateState>.Ok(
            await service.UpdateRegistrationGateAsync(actorId, req, ct)), ct);
    }
}
