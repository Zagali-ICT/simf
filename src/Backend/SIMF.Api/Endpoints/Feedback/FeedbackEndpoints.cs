// Tests: SIMF.Api.Tests/FeedbackRatingsTests.cs
using FastEndpoints;
using FluentValidation;
using SIMF.Api.Endpoints.Admin;
using SIMF.Api.RequestContext;
using SIMF.Application.Feedback.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Feedback;

namespace SIMF.Api.Endpoints.Feedback;

// App-facing dynamic rating endpoints + the admin responses/KPI viewer. The
// attendee fetches the form for a type (+ optional target), then submits;
// validation of the dynamic shape lives in the service.

public sealed class SubmitRatingRequestValidator : Validator<SubmitRatingRequest>
{
    public SubmitRatingRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => x.RatingTypeId != Guid.Empty || !string.IsNullOrWhiteSpace(x.Code))
            .WithMessage("A rating type id or code is required.");

        RuleFor(x => x.OverallStars!.Value)
            .InclusiveBetween(1, 5).When(x => x.OverallStars is not null)
            .WithMessage("Stars must be between 1 and 5.");

        // MaximumLength MUST stay aligned with EF HasMaxLength(2000) on RatingResponse.Comment.
        RuleFor(x => x.Comment)
            .MaximumLength(2000)
            .When(x => x.Comment is not null);

        RuleForEach(x => x.Answers).ChildRules(answer =>
        {
            answer.RuleFor(a => a.QuestionId).NotEmpty()
                .WithMessage("Each answer must reference a question.");
            answer.RuleFor(a => a.Stars).InclusiveBetween(1, 5)
                .WithMessage("Each score must be between 1 and 5.");
        });
    }
}

/// <summary>GET the dynamic rating form for a type (+ optional target),
/// including the attendee's existing submission for prefill.</summary>
public sealed class GetRatingFormEndpoint(IRatingFormService service)
    : Endpoint<RatingFormRequest, ApiResult<RatingFormView>>
{
    public override void Configure()
    {
        Get("/app/feedback/form");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Feedback");
    }

    public override async Task HandleAsync(RatingFormRequest req, CancellationToken ct)
    {
        var userId = User.ActorId();
        var view = await service.GetFormAsync(userId, req, ct);
        await Send.OkAsync(ApiResult<RatingFormView>.Ok(view), ct);
    }
}

/// <summary>POST the attendee's submission (overall + per-question answers +
/// comment). Upserts one submission per user per (type, target).</summary>
public sealed class SubmitRatingEndpoint(IRatingFormService service)
    : Endpoint<SubmitRatingRequest, ApiResult<RatingSubmissionView>>
{
    public override void Configure()
    {
        Post("/app/feedback/submit");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Feedback");
    }

    public override async Task HandleAsync(SubmitRatingRequest req, CancellationToken ct)
    {
        var userId = User.ActorId();
        var view = await service.SubmitAsync(userId, req, ct);
        await Send.OkAsync(ApiResult<RatingSubmissionView>.Ok(view), ct);
    }
}

/// <summary>Admin list of rating responses + the average-overall headline.</summary>
public sealed class ListAdminRatingsEndpoint(IAdminRatingResponseService service)
    : Endpoint<GridQuery, ApiResult<AdminRatingResponsesPage>>
{
    public override void Configure()
    {
        Post("/admin/feedback/ratings");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Ratings.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<AdminRatingResponsesPage>.Ok(
            await service.ListResponsesAsync(req, ct)), ct);
}

/// <summary>Admin rating KPIs — per type response count, overall average and the
/// per-question averages.</summary>
public sealed class RatingKpiEndpoint(IAdminRatingResponseService service)
    : EndpointWithoutRequest<ApiResult<AdminRatingKpiView>>
{
    public override void Configure()
    {
        Get("/admin/feedback/ratings/kpi");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Ratings.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<AdminRatingKpiView>.Ok(
            await service.GetKpiAsync(ct)), ct);
}
