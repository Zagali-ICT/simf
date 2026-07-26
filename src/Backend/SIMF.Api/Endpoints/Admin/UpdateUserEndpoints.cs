// Tests: SIMF.Api.Tests/AdminUpdateUserTests.cs
using System.Security.Claims;
using FastEndpoints;
using FluentValidation;
using SIMF.Api.Endpoints.Auth.Validators;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>P1.3 (D-214) — the route id + body for the visitor edit endpoint.
/// Mirrors <see cref="RejectRouteRequest"/>: the user id comes from the path,
/// the editable fields from the JSON body.</summary>
public sealed class UpdateVisitorRouteRequest
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public Guid? ProfileTypeId { get; set; }

    /// <summary>Bi-Meeting rework — the admin-assigned speaker/delegation meeting flags.</summary>
    public bool AllowsSpeakerMeeting { get; set; }
    public bool AllowsDelegationMeeting { get; set; }

    /// <summary>B22 — optional ISO alpha-2 nationality correction; null / empty leaves
    /// the stored nationality untouched. See
    /// <see cref="AdminUpdateVisitorRequest.NationalityCode"/>.</summary>
    public string? NationalityCode { get; set; }
}

/// <summary>P1.3 (D-214) — the route id + body for the Other edit endpoint.</summary>
public sealed class UpdateOtherRouteRequest
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public Guid ProfileTypeId { get; set; }

    /// <summary>Bi-Meeting rework — the admin-assigned speaker/delegation meeting flags.</summary>
    public bool AllowsSpeakerMeeting { get; set; }
    public bool AllowsDelegationMeeting { get; set; }

    /// <summary>B22 — optional ISO alpha-2 nationality correction; null / empty leaves
    /// the stored nationality untouched.</summary>
    public string? NationalityCode { get; set; }
}

/// <summary>Validates the visitor edit (Email + DisplayName; tier optional).</summary>
public sealed class UpdateVisitorRouteRequestValidator : Validator<UpdateVisitorRouteRequest>
{
    public UpdateVisitorRouteRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty().Bilingual("Email is required.", "البريد الإلكتروني مطلوب.")
            .EmailAddress().Bilingual(
                "A valid email address is required.",
                "يجب إدخال بريد إلكتروني صالح.")
            .MaximumLength(256);

        RuleFor(request => request.DisplayName)
            .NotEmpty().Bilingual("Display name is required.", "الاسم المعروض مطلوب.")
            .MinimumLength(2).Bilingual(
                "Display name must be at least 2 characters.",
                "يجب أن يتكوّن الاسم المعروض من حرفين على الأقل.")
            .MaximumLength(128).Bilingual(
                "Display name must be at most 128 characters.",
                "يجب ألا يتجاوز الاسم المعروض 128 حرفًا.");
    }
}

/// <summary>Validates the Other edit (Email + DisplayName + mandatory tier).</summary>
public sealed class UpdateOtherRouteRequestValidator : Validator<UpdateOtherRouteRequest>
{
    public UpdateOtherRouteRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty().Bilingual("Email is required.", "البريد الإلكتروني مطلوب.")
            .EmailAddress().Bilingual(
                "A valid email address is required.",
                "يجب إدخال بريد إلكتروني صالح.")
            .MaximumLength(256);

        RuleFor(request => request.DisplayName)
            .NotEmpty().Bilingual("Display name is required.", "الاسم المعروض مطلوب.")
            .MinimumLength(2).Bilingual(
                "Display name must be at least 2 characters.",
                "يجب أن يتكوّن الاسم المعروض من حرفين على الأقل.")
            .MaximumLength(128).Bilingual(
                "Display name must be at most 128 characters.",
                "يجب ألا يتجاوز الاسم المعروض 128 حرفًا.");

        RuleFor(request => request.ProfileTypeId)
            .NotEqual(Guid.Empty).Bilingual(
                "A profile type is required.",
                "نوع الملف الشخصي مطلوب.");
    }
}

/// <summary>P1.3 (D-214) — <c>PUT /api/v1/admin/visitors/{id}</c>. Edits a
/// visitor's login email, display name, and optional tier. Gated by
/// <c>Visitors.Edit</c>. An email change rolls the security stamp + revokes
/// sessions (handled in the service).</summary>
public sealed class UpdateVisitorEndpoint(IAdminUserProvisioningService service)
    : Endpoint<UpdateVisitorRouteRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Put("/admin/visitors/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Edit a visitor's email, display name, and optional tier.");
    }

    public override async Task HandleAsync(UpdateVisitorRouteRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await service.UpdateVisitorAsync(actorId, req.Id,
            new AdminUpdateVisitorRequest
            {
                Email = req.Email,
                DisplayName = req.DisplayName,
                ProfileTypeId = req.ProfileTypeId,
                AllowsSpeakerMeeting = req.AllowsSpeakerMeeting,
                AllowsDelegationMeeting = req.AllowsDelegationMeeting,
                // B22 — carry the optional nationality correction through the
                // route DTO -> contract DTO hand-off.
                NationalityCode = req.NationalityCode,
            }, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

/// <summary>P1.3 (D-214) — <c>PUT /api/v1/admin/others/{id}</c>. Edits a
/// partner-side (Other) account. Gated by <c>Others.Edit</c>.</summary>
public sealed class UpdateOtherEndpoint(IAdminUserProvisioningService service)
    : Endpoint<UpdateOtherRouteRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Put("/admin/others/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Others.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Edit a partner (Other) account's email, display name, and subtype.");
    }

    public override async Task HandleAsync(UpdateOtherRouteRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await service.UpdateOtherAsync(actorId, req.Id,
            new AdminUpdateOtherRequest
            {
                Email = req.Email,
                DisplayName = req.DisplayName,
                ProfileTypeId = req.ProfileTypeId,
                AllowsSpeakerMeeting = req.AllowsSpeakerMeeting,
                AllowsDelegationMeeting = req.AllowsDelegationMeeting,
                // B22 — carry the optional nationality correction through the
                // route DTO -> contract DTO hand-off.
                NationalityCode = req.NationalityCode,
            }, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}
