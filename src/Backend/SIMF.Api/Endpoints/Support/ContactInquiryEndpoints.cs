// Tests: SIMF.Api.Tests/ContactInquiryTests.cs
using FastEndpoints;
using FluentValidation;
using SIMF.Api.Endpoints.Admin;
using SIMF.Api.RequestContext;
using SIMF.Application.Support.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Support;

namespace SIMF.Api.Endpoints.Support;

/// <summary>Validates the public contact-form submit (Figma 1388:7567). Lengths
/// mirror EF <c>HasMaxLength</c> on <c>ContactInquiry</c> (Name 120 / Email 256 /
/// Message 4000).</summary>
public sealed class SubmitContactInquiryValidator : Validator<SubmitContactInquiryRequest>
{
    public SubmitContactInquiryValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Message).NotEmpty().MaximumLength(4000);
    }
}

/// <summary>Public, anonymous submit of a "تواصل معنا / Contact us" message.
/// Rate-limited (anonymous write); captures the signed-in submitter when a
/// bearer token is present.</summary>
public sealed class SubmitContactInquiryEndpoint(IContactInquiryService service)
    : Endpoint<SubmitContactInquiryRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Post("/app/contact-inquiry");
        AllowAnonymous();
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Public");
    }

    public override async Task HandleAsync(SubmitContactInquiryRequest req, CancellationToken ct)
    {
        Guid? submittedBy = User.ActorIdOrNull();
        await service.SubmitAsync(req, submittedBy, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

/// <summary>CP inbox — list submitted inquiries (open first, newest first).</summary>
public sealed class ListContactInquiriesEndpoint(IContactInquiryService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminContactInquiryRow>>>
{
    public override void Configure()
    {
        Post("/admin/contact-inquiries/list");
        Policies(
            PermissionCatalog.PolicyFor(PermissionCatalog.ContactInquiries.View),
            nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminContactInquiryRow>>.Ok(
            await service.ListAsync(req, ct)), ct);
}

public sealed class MarkContactInquiryHandledRequest
{
    public bool Handled { get; set; } = true;
}

/// <summary>CP — mark an inquiry handled / reopened.</summary>
public sealed class MarkContactInquiryHandledEndpoint(IContactInquiryService service)
    : Endpoint<MarkContactInquiryHandledRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Post("/admin/contact-inquiries/{id:guid}/handled");
        Policies(
            PermissionCatalog.PolicyFor(PermissionCatalog.ContactInquiries.Manage),
            nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(MarkContactInquiryHandledRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await service.MarkHandledAsync(actorId, Route<Guid>("id"), req.Handled, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}
