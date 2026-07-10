// Tests: SIMF.Api.Tests/AdminChangeAccountTypeTests.cs
using System.Security.Claims;
using FastEndpoints;
using FluentValidation;
using SIMF.Api.Endpoints.Auth.Validators;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>D-728 (owner item 9) — the route id + body for the change-type
/// endpoint. The account id comes from the path, the target profile type from
/// the JSON body.</summary>
public sealed class ChangeAccountTypeRouteRequest
{
    public Guid Id { get; set; }
    public Guid NewProfileTypeId { get; set; }
}

/// <summary>Validates the change-type request (a target profile type is
/// required).</summary>
public sealed class ChangeAccountTypeRouteRequestValidator
    : Validator<ChangeAccountTypeRouteRequest>
{
    public ChangeAccountTypeRouteRequestValidator()
    {
        RuleFor(request => request.NewProfileTypeId)
            .NotEqual(Guid.Empty).Bilingual(
                "A target profile type is required.",
                "نوع الملف الشخصي المستهدف مطلوب.");
    }
}

/// <summary>D-728 (owner item 9) — <c>POST /api/v1/admin/accounts/{id}/change-type</c>.
/// Flips an existing account between the audience (Visitor) and partner (Other)
/// scope by reassigning its profile type to one in the opposite scope. Gated by
/// the dedicated <c>Accounts.ChangeType</c> code. The flip rolls the security
/// stamp + revokes sessions (handled in the service) because the new type
/// re-sources the app permission claims; approval state is left unchanged.</summary>
public sealed class ChangeAccountTypeEndpoint(IAdminUserProvisioningService service)
    : Endpoint<ChangeAccountTypeRouteRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Post("/admin/accounts/{id:guid}/change-type");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Accounts.ChangeType),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Change an account's type between Visitor and Other.");
    }

    public override async Task HandleAsync(
        ChangeAccountTypeRouteRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await service.ChangeAccountTypeAsync(
            actorId, req.Id, req.NewProfileTypeId, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}
