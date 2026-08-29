// Tests: SIMF.Api.Tests/WalkInModeSettingsTests.cs
using FastEndpoints;
using SIMF.Api.RequestContext;
using SIMF.Application.Configuration.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Configuration;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>The CP "Walk-in mode" page: read the two desk modes as an admin
/// sees them — effective values, what deployment configuration alone says, and
/// whether either is currently overridden. Gated by WalkInMode.View.</summary>
public sealed class GetAdminWalkInModeEndpoint(IWalkInModeSettings walkInMode)
    : EndpointWithoutRequest<ApiResult<WalkInModeSettingsResponse>>
{
    public override void Configure()
    {
        Get("/admin/walk-in-mode");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.WalkInMode.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<WalkInModeSettingsResponse>.Ok(
            await walkInMode.GetAsync(ct)), ct);
}

/// <summary>What the CP walk-in form needs to decide which fields to demand.
/// Gated by the permission to USE the desk, not the permission to change the
/// modes: an operator who may register a walk-in must be able to render the
/// right form, and most of them cannot open the settings page.</summary>
public sealed class GetDeskWalkInModeEndpoint(IWalkInModeSettings walkInMode)
    : EndpointWithoutRequest<ApiResult<WalkInDeskModeResponse>>
{
    public override void Configure()
    {
        Get("/admin/walk-in-mode/desk");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.RegisterOnsite),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<WalkInDeskModeResponse>.Ok(
            await walkInMode.GetDeskAsync(ct)), ct);
}

/// <summary>The staff-app twin of the desk read, for the tablet
/// register-visitor screen. Same permission, same payload, different surface —
/// mirroring how register-onsite itself is exposed on both.</summary>
public sealed class GetStaffWalkInModeEndpoint(IWalkInModeSettings walkInMode)
    : EndpointWithoutRequest<ApiResult<WalkInDeskModeResponse>>
{
    public override void Configure()
    {
        Get("/app/staff/walk-in-mode");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.RegisterOnsite),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Staff");
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<WalkInDeskModeResponse>.Ok(
            await walkInMode.GetDeskAsync(ct)), ct);
}

/// <summary>Turns a walk-in desk mode on or off without a deploy. A null field
/// CLEARS that override and hands the mode back to deployment configuration,
/// which is the only way an admin can undo their own change.
///
/// <para>Its own permission (WalkInMode.Manage) rather than Configuration.Edit:
/// auto-approve relaxes an approval gate, so granting somebody the run of the
/// configuration page should not hand them that switch by accident.</para></summary>
public sealed class SaveAdminWalkInModeEndpoint(IWalkInModeSettings walkInMode)
    : Endpoint<AdminUpdateWalkInModeRequest, ApiResult<WalkInModeSettingsResponse>>
{
    public override void Configure()
    {
        Post("/admin/walk-in-mode");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.WalkInMode.Manage),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(
        AdminUpdateWalkInModeRequest req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<WalkInModeSettingsResponse>.Ok(
            await walkInMode.SaveAsync(User.ActorId(), req, ct)), ct);
}
