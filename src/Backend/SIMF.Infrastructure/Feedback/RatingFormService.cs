// Tests: SIMF.Api.Tests/FeedbackRatingsTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Feedback.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Feedback;
using SIMF.Domain.Feedback;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Feedback;

/// <summary>App-facing dynamic rating service. <see cref="GetFormAsync"/> returns
/// the type's config + active grouped/flat questions + the attendee's existing
/// submission (prefill); <see cref="SubmitAsync"/> validates against the type's
/// config and upserts on <c>(UserId, RatingTypeId, TargetId)</c> (the unique index
/// guarantees one submission per user per target). Mirrors the old RatingService
/// upsert shape, generalised to many configurable types.</summary>
internal sealed class RatingFormService(
    SimfAppDbContext dbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<RatingFormService> logger) : IRatingFormService
{
    private const int MinStars = 1;
    private const int MaxStars = 5;
    private const int CommentMaxLength = 2000;

    public async Task<RatingFormView> GetFormAsync(
        Guid userId, RatingFormRequest request, CancellationToken cancellationToken = default)
    {
        var type = await ResolveTypeAsync(request.RatingTypeId, request.Code, cancellationToken);
        var targetId = await ResolveTargetAsync(type, request.TargetId, cancellationToken);

        var groups = await dbContext.RatingQuestionGroups.AsNoTracking()
            .Where(g => g.RatingTypeId == type.Id && g.IsActive)
            .OrderBy(g => g.DisplayOrder).ThenBy(g => g.Name)
            .ToListAsync(cancellationToken);
        var activeGroupIds = groups.Select(g => g.Id).ToHashSet();

        var questions = await dbContext.RatingQuestions.AsNoTracking()
            .Where(q => q.RatingTypeId == type.Id && q.IsActive)
            .OrderBy(q => q.DisplayOrder).ThenBy(q => q.Text)
            .ToListAsync(cancellationToken);

        var grouped = groups
            .Select(g => new RatingFormGroup(
                g.Id, g.Name, g.NameArabic, g.DisplayOrder,
                questions
                    .Where(q => q.RatingQuestionGroupId == g.Id)
                    .Select(ToFormQuestion).ToList()))
            .Where(g => g.Questions.Count > 0)
            .ToList();

        // Questions with no active group (null FK or a soft-deleted group) render flat.
        var ungrouped = questions
            .Where(q => q.RatingQuestionGroupId is null
                || !activeGroupIds.Contains(q.RatingQuestionGroupId.Value))
            .Select(ToFormQuestion)
            .ToList();

        var existing = await LoadExistingAsync(userId, type.Id, targetId, cancellationToken);

        return new RatingFormView(
            type.Id, type.Code, type.Name, type.NameArabic, type.Scope,
            type.HasOverallStars, type.AllowComment, type.CommentLabel, type.CommentLabelArabic,
            type.Scope == RatingScope.Global ? null : targetId,
            grouped, ungrouped, existing);
    }

    public async Task<RatingSubmissionView> SubmitAsync(
        Guid userId, SubmitRatingRequest request, CancellationToken cancellationToken = default)
    {
        var type = await ResolveTypeAsync(
            request.RatingTypeId == Guid.Empty ? null : request.RatingTypeId,
            request.Code, cancellationToken);
        var targetId = await ResolveTargetAsync(type, request.TargetId, cancellationToken);

        var activeQuestions = await dbContext.RatingQuestions.AsNoTracking()
            .Where(q => q.RatingTypeId == type.Id && q.IsActive)
            .Select(q => new { q.Id, q.IsRequired })
            .ToListAsync(cancellationToken);
        var questionIds = activeQuestions.Select(q => q.Id).ToHashSet();
        var requiredIds = activeQuestions.Where(q => q.IsRequired).Select(q => q.Id).ToHashSet();

        var overall = ValidateOverall(type, request.OverallStars);
        var answers = ValidateAnswers(request.Answers, requiredIds, questionIds);
        var comment = type.AllowComment ? NullIfBlank(request.Comment) : null;
        if (comment is not null && comment.Length > CommentMaxLength)
        {
            throw Invalid(
                $"Comment must be {CommentMaxLength} characters or less.",
                $"يجب ألا يتجاوز التعليق {CommentMaxLength} حرفاً.");
        }

        var now = timeProvider.GetUtcNow();

        var existing = await dbContext.RatingResponses
            .Include(r => r.Answers)
            .SingleOrDefaultAsync(
                r => r.UserId == userId && r.RatingTypeId == type.Id && r.TargetId == targetId,
                cancellationToken);

        RatingResponse response;
        bool revised;
        if (existing is not null)
        {
            existing.OverallStars = overall;
            existing.Comment = comment;
            existing.IsActive = true;
            existing.UpdatedAt = now;
            dbContext.RatingAnswers.RemoveRange(existing.Answers);
            existing.Answers = BuildAnswers(existing.Id, answers);
            response = existing;
            revised = true;
        }
        else
        {
            response = new RatingResponse
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                RatingTypeId = type.Id,
                TargetId = targetId,
                OverallStars = overall,
                Comment = comment,
                CreatedAt = now,
                IsActive = true,
            };
            response.Answers = BuildAnswers(response.Id, answers);
            dbContext.RatingResponses.Add(response);
            revised = false;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = revised ? AuditEvents.RatingRevised : AuditEvents.RatingSubmitted,
            Outcome = AuditOutcome.Success,
            ActorUserId = userId,
            Detail = $"responseId={response.Id}; type={type.Code}; target={response.TargetId}; " +
                     $"overall={response.OverallStars}; answers={answers.Count}",
        }, cancellationToken);

        logger.LogInformation(
            "User {UserId} {Action} rating {ResponseId} for type {Code} ({Answers} answers)",
            userId, revised ? "revised" : "submitted", response.Id, type.Code, answers.Count);

        return ToView(response, answers);
    }

    // -- internals ------------------------------------------------------------

    private async Task<RatingType> ResolveTypeAsync(
        Guid? id, string? code, CancellationToken cancellationToken)
    {
        // Neither identifier supplied is a malformed request (400), distinct from a
        // well-formed lookup that finds nothing (404).
        if (id is null && string.IsNullOrWhiteSpace(code))
        {
            throw Invalid(
                "A rating type id or code is required.",
                "يجب تحديد نوع التقييم.");
        }

        RatingType? type = null;
        if (id is { } typeId)
        {
            type = await dbContext.RatingTypes
                .SingleOrDefaultAsync(t => t.Id == typeId && t.IsActive, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(code))
        {
            var slug = code.Trim();
            type = await dbContext.RatingTypes
                .SingleOrDefaultAsync(t => t.Code == slug && t.IsActive, cancellationToken);
        }

        return type ?? throw new ApiException(
            ErrorCodes.RatingTypeNotFound, 404,
            "The rating type was not found.", "لم يتم العثور على نوع التقييم.");
    }

    private async Task<Guid> ResolveTargetAsync(
        RatingType type, Guid? requestedTarget, CancellationToken cancellationToken)
    {
        if (type.Scope == RatingScope.Global)
        {
            // Sentinel so the (UserId, TypeId, TargetId) unique index stays uniform.
            return Guid.Empty;
        }

        if (requestedTarget is not { } target || target == Guid.Empty)
        {
            throw new ApiException(
                ErrorCodes.RatingTargetRequired, 400,
                "A target is required to rate this.", "يجب تحديد العنصر المراد تقييمه.");
        }

        var exists = await dbContext.Sessions
            .AnyAsync(s => s.Id == target && s.IsActive, cancellationToken);
        if (!exists)
        {
            throw new ApiException(
                ErrorCodes.RatingTargetNotFound, 404,
                "The session to rate was not found.", "لم يتم العثور على الجلسة المراد تقييمها.");
        }
        return target;
    }

    private int? ValidateOverall(RatingType type, int? overall)
    {
        if (!type.HasOverallStars)
        {
            return null; // overall not collected for this type
        }
        if (overall is not { } stars)
        {
            throw Invalid("An overall rating is required.", "التقييم العام مطلوب.");
        }
        if (stars is < MinStars or > MaxStars)
        {
            throw Invalid("Stars must be between 1 and 5.", "يجب أن يكون التقييم بين 1 و 5.");
        }
        return stars;
    }

    private IReadOnlyList<RatingAnswerInput> ValidateAnswers(
        IEnumerable<RatingAnswerInput> incoming,
        HashSet<Guid> requiredIds,
        HashSet<Guid> questionIds)
    {
        var seen = new HashSet<Guid>();
        var answers = new List<RatingAnswerInput>();
        foreach (var answer in incoming ?? Enumerable.Empty<RatingAnswerInput>())
        {
            if (!questionIds.Contains(answer.QuestionId))
            {
                throw Invalid("An answer refers to an unknown question.",
                    "إحدى الإجابات تشير إلى سؤال غير معروف.");
            }
            if (!seen.Add(answer.QuestionId))
            {
                throw Invalid("A question was answered more than once.",
                    "تم الإجابة على أحد الأسئلة أكثر من مرة.");
            }
            if (answer.Stars is < MinStars or > MaxStars)
            {
                throw Invalid("Each score must be between 1 and 5.",
                    "يجب أن تكون كل درجة بين 1 و 5.");
            }
            answers.Add(answer);
        }

        // Required-question coverage.
        if (!requiredIds.IsSubsetOf(seen))
        {
            throw Invalid("Please answer all required questions.",
                "يرجى الإجابة على جميع الأسئلة المطلوبة.");
        }
        return answers;
    }

    private async Task<RatingExistingSubmission?> LoadExistingAsync(
        Guid userId, Guid typeId, Guid targetId, CancellationToken cancellationToken)
    {
        var existing = await dbContext.RatingResponses.AsNoTracking()
            .Include(r => r.Answers)
            .SingleOrDefaultAsync(
                r => r.UserId == userId && r.RatingTypeId == typeId && r.TargetId == targetId,
                cancellationToken);
        if (existing is null)
        {
            return null;
        }
        return new RatingExistingSubmission(
            existing.OverallStars,
            existing.Comment,
            existing.Answers
                .Select(a => new RatingExistingAnswer(a.RatingQuestionId, a.Stars))
                .ToList());
    }

    private static List<RatingAnswer> BuildAnswers(Guid responseId, IReadOnlyList<RatingAnswerInput> answers) =>
        answers.Select(a => new RatingAnswer
        {
            Id = Guid.NewGuid(),
            RatingResponseId = responseId,
            RatingQuestionId = a.QuestionId,
            Stars = a.Stars,
        }).ToList();

    private static RatingSubmissionView ToView(RatingResponse r, IReadOnlyList<RatingAnswerInput> answers) =>
        new(r.Id, r.RatingTypeId,
            r.TargetId == Guid.Empty ? null : r.TargetId,
            r.OverallStars, r.Comment, r.CreatedAt, r.UpdatedAt, answers);

    private static RatingFormQuestion ToFormQuestion(RatingQuestion q) =>
        new(q.Id, q.Text, q.TextArabic, q.IsRequired, q.DisplayOrder);

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ApiException Invalid(string en, string ar) =>
        new(ErrorCodes.RatingInvalid, 400, en, ar);
}
