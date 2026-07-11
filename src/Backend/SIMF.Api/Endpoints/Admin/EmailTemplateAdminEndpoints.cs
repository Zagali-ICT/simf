// Tests: SIMF.Api.Tests/EmailTemplateAdminTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.Email;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Email;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>D-735 — admin list/read/edit/reset/preview over the transactional
/// email templates. The DB holds only overrides; the catalogue backs every read
/// so the grid always shows all six templates. The <c>{type}</c> route segment is
/// the <see cref="EmailTemplateType"/> name (case-insensitive).</summary>
public sealed class ListEmailTemplatesEndpoint(IAdminEmailTemplateService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminEmailTemplateSummary>>>
{
    public override void Configure()
    {
        Post("/admin/email/templates/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.EmailTemplates.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminEmailTemplateSummary>>.Ok(
            await service.ListAsync(req, ct)), ct);
}

public sealed class EmailTemplateRoute
{
    public string Type { get; set; } = string.Empty;
}

public sealed class GetEmailTemplateEndpoint(IAdminEmailTemplateService service)
    : Endpoint<EmailTemplateRoute, ApiResult<AdminEmailTemplateDetail>>
{
    public override void Configure()
    {
        Get("/admin/email/templates/{type}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.EmailTemplates.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(EmailTemplateRoute req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<AdminEmailTemplateDetail>.Ok(
            await service.GetAsync(ParseType(req.Type), ct)), ct);

    /// <summary>Parses the route segment to a defined <see cref="EmailTemplateType"/>
    /// or throws a 404 — shared by the read/edit/reset/preview endpoints.</summary>
    internal static EmailTemplateType ParseType(string raw) =>
        Enum.TryParse<EmailTemplateType>(raw, ignoreCase: true, out var type)
        && Enum.IsDefined(type)
            ? type
            : throw new ApiException(
                ErrorCodes.EmailTemplateNotFound, 404,
                "Email template not found.",
                "لم يتم العثور على قالب البريد الإلكتروني.");
}

public sealed class UpdateEmailTemplateRoute : UpdateEmailTemplateRequest
{
    public string Type { get; set; } = string.Empty;
}

public sealed class UpdateEmailTemplateEndpoint(IAdminEmailTemplateService service)
    : Endpoint<UpdateEmailTemplateRoute, ApiResult<AdminEmailTemplateDetail>>
{
    public override void Configure()
    {
        Put("/admin/email/templates/{type}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.EmailTemplates.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(UpdateEmailTemplateRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        await Send.OkAsync(ApiResult<AdminEmailTemplateDetail>.Ok(
            await service.UpdateAsync(
                actorId, GetEmailTemplateEndpoint.ParseType(req.Type), req, ct)), ct);
    }
}

public sealed class ResetEmailTemplateEndpoint(IAdminEmailTemplateService service)
    : Endpoint<EmailTemplateRoute, ApiResult<AdminEmailTemplateDetail>>
{
    public override void Configure()
    {
        Post("/admin/email/templates/{type}/reset");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.EmailTemplates.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(EmailTemplateRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        await Send.OkAsync(ApiResult<AdminEmailTemplateDetail>.Ok(
            await service.ResetAsync(
                actorId, GetEmailTemplateEndpoint.ParseType(req.Type), ct)), ct);
    }
}

public sealed class PreviewEmailTemplateRoute : PreviewEmailTemplateRequest
{
    public string Type { get; set; } = string.Empty;
}

public sealed class PreviewEmailTemplateEndpoint(IAdminEmailTemplateService service)
    : Endpoint<PreviewEmailTemplateRoute, ApiResult<EmailTemplatePreviewResult>>
{
    public override void Configure()
    {
        Post("/admin/email/templates/{type}/preview");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.EmailTemplates.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        // No rate limit: preview is a read-only, no-side-effect render the CP
        // editor calls on every keystroke — it must NOT drain the shared per-IP
        // "auth" bucket that sign-in / OTP use (cf. D-731). The View permission
        // already gates it to admins.
        Tags("Admin");
    }

    public override async Task HandleAsync(PreviewEmailTemplateRoute req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<EmailTemplatePreviewResult>.Ok(
            service.Preview(GetEmailTemplateEndpoint.ParseType(req.Type), req)), ct);
}
