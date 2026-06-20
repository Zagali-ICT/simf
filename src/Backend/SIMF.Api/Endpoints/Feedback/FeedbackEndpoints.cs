// Tests: SIMF.Api.Tests/FeedbackRatingsTests.cs
using System.Security.Claims;
using FastEndpoints;
using FluentValidation;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.Feedback.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Feedback;

namespace SIMF.Api.Endpoints.Feedback;

/// <summary>D-199 (Mockup screen 40 "Rate the Forum") — the attendee submits
/// or revises their single overall rating. Authed + approved account, exactly
/// like the seat-reservation / session-question attendee writes; one-per-user
/// upsert is enforced in the service + a unique index.</summary>
public sealed class RateRequestValidator : Validator<RateRequest>
{
    public RateRequestValidator()
    {
        RuleFor(x => x.Stars)
            .InclusiveBetween(1, 5)
            .WithMessage("Stars must be between 1 and 5.");

        // Per-element scores (Figma "قيّم العناصر") — each optional, but when
        // present must be a valid 1–5, exactly like the overall stars.
        RuleFor(x => x.OrganizationStars!.Value)
            .InclusiveBetween(1, 5).When(x => x.OrganizationStars is not null)
            .WithMessage("Organization stars must be between 1 and 5.");
        RuleFor(x => x.ContentStars!.Value)
            .InclusiveBetween(1, 5).When(x => x.ContentStars is not null)
            .WithMessage("Content stars must be between 1 and 5.");
        RuleFor(x => x.AppStars!.Value)
            .InclusiveBetween(1, 5).When(x => x.AppStars is not null)
            .WithMessage("App stars must be between 1 and 5.");
        RuleFor(x => x.VenueStars!.Value)
            .InclusiveBetween(1, 5).When(x => x.VenueStars is not null)
            .WithMessage("Venue stars must be between 1 and 5.");

        // MaximumLength MUST stay aligned with EF HasMaxLength(2000) on Rating.Comment.
        RuleFor(x => x.Comment)
            .MaximumLength(2000)
            .When(x => x.Comment is not null);
    }
}

public sealed class RateFeedbackEndpoint(IRatingService service)
    : Endpoint<RateRequest, ApiResult<RatingView>>
{
    public override void Configure()
    {
        Post("/app/feedback/rate");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Feedback");
    }

    public override async Task HandleAsync(RateRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var view = await service.RateAsync(userId, req, ct);
        await Send.OkAsync(ApiResult<RatingView>.Ok(view), ct);
    }
}

/// <summary>D-199 — admin list of ratings + the average-stars headline.
/// Administrator-only + approved account, mirroring the other admin lists.</summary>
public sealed class ListAdminRatingsEndpoint(IRatingService service)
    : Endpoint<GridQuery, ApiResult<AdminRatingsPage>>
{
    public override void Configure()
    {
        Post("/admin/feedback/ratings");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Ratings.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<AdminRatingsPage>.Ok(
            await service.ListAllAsync(req, ct)), ct);
}
