// Tests: SIMF.Api.Tests/AdminUpdateUserTests.cs
//        SIMF.Api.Tests/AdminAccountMobileTests.cs (the optional
//        SaudiMobile / InternationalMobile correction: correct, canonicalise,
//        omit-means-unchanged, malformed rolls the whole edit back, permission)
using FastEndpoints;
using FluentValidation;
using SIMF.Api.Endpoints.Account.Validators;
using SIMF.Api.Endpoints.Auth.Validators;
using SIMF.Api.RequestContext;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>Binds {id} + body via a derived route that INHERITS the
/// contract (see <c>UpdateHallRoute</c>). It used to re-declare the
/// contract's fields and the endpoint hand-copied them across, which is how
/// sessions, gates, profile types and several others silently dropped a field
/// on PUT. Passing the bound request straight through makes that drop
/// impossible.</summary>
public sealed class UpdateVisitorRouteRequest : AdminUpdateVisitorRequest
{
    public Guid Id { get; set; }
}

/// <summary>Binds {id} + body via a derived route that INHERITS the
/// contract (see <c>UpdateHallRoute</c>). It used to re-declare the
/// contract's fields and the endpoint hand-copied them across, which is how
/// sessions, gates, profile types and several others silently dropped a field
/// on PUT. Passing the bound request straight through makes that drop
/// impossible.</summary>
public sealed class UpdateOtherRouteRequest : AdminUpdateOtherRequest
{
    public Guid Id { get; set; }
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

        // The optional mobile correction validates IDENTICALLY to
        // the self-service rule: the shapes are the shared
        // UpsertUserProfileRequestValidator predicates (one source of truth), and
        // an omitted value means "leave the stored number alone", so a desk that
        // only edits the email never has to re-send the phone.
        When(request => !string.IsNullOrWhiteSpace(request.SaudiMobile), () =>
        {
            RuleFor(request => request.SaudiMobile!)
                .Must(UpsertUserProfileRequestValidator.IsStandardSaudiMobile)
                .Bilingual(
                    "The Saudi mobile must be 05XXXXXXXX or +9665XXXXXXXX.",
                    "يجب أن يكون رقم الجوال السعودي بصيغة 05XXXXXXXX أو +9665XXXXXXXX.");
        });

        When(request => !string.IsNullOrWhiteSpace(request.InternationalMobile), () =>
        {
            RuleFor(request => request.InternationalMobile!)
                .Must(UpsertUserProfileRequestValidator.IsStandardInternationalMobile)
                .Bilingual(
                    "The international mobile must be in the +<country code><number> (E.164) format.",
                    "يجب أن يكون رقم الجوال الدولي بالصيغة الدولية ‎+‎ يليها رمز الدولة والرقم (E.164).");
        });
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

        // The same optional mobile correction as the visitor desk.
        When(request => !string.IsNullOrWhiteSpace(request.SaudiMobile), () =>
        {
            RuleFor(request => request.SaudiMobile!)
                .Must(UpsertUserProfileRequestValidator.IsStandardSaudiMobile)
                .Bilingual(
                    "The Saudi mobile must be 05XXXXXXXX or +9665XXXXXXXX.",
                    "يجب أن يكون رقم الجوال السعودي بصيغة 05XXXXXXXX أو +9665XXXXXXXX.");
        });

        When(request => !string.IsNullOrWhiteSpace(request.InternationalMobile), () =>
        {
            RuleFor(request => request.InternationalMobile!)
                .Must(UpsertUserProfileRequestValidator.IsStandardInternationalMobile)
                .Bilingual(
                    "The international mobile must be in the +<country code><number> (E.164) format.",
                    "يجب أن يكون رقم الجوال الدولي بالصيغة الدولية ‎+‎ يليها رمز الدولة والرقم (E.164).");
        });
    }
}

/// <summary><c>PUT /api/v1/admin/visitors/{id}</c>. Edits a
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
        var actorId = User.ActorId();
        await service.UpdateVisitorAsync(actorId, req.Id,
            req, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

/// <summary><c>PUT /api/v1/admin/others/{id}</c>. Edits a
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
        var actorId = User.ActorId();
        await service.UpdateOtherAsync(actorId, req.Id,
            req, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}
