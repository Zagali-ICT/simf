// Tests: SIMF.Api.Tests/FeedbackRatingsTests.cs
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Feedback.Abstractions;
using SIMF.Common;
using SIMF.Common.Grids;
using SIMF.Contracts.Feedback;
using SIMF.Domain.Feedback;
using SIMF.Infrastructure.Common.Grids;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Feedback;

/// <summary>Read-only admin view over submitted rating responses: the paged grid
/// (with the overall-average headline) and the per-type / per-question KPI
/// aggregates. Responses are owned by the attendees who submit them, so this is a
/// viewer only — no create/edit here.</summary>
internal sealed class AdminRatingResponseService(
    SimfAppDbContext dbContext) : IAdminRatingResponseService
{
    /// <summary>The grid contract for /admin/ratings: one entry per key
    /// RatingsList.razor can send, as both its filter and its sort.</summary>
    private static readonly GridColumns<RatingResponse> Columns = new GridColumns<RatingResponse>()
        .Add("ratingTypeId", response => response.RatingTypeId)
        .Add("overall", response => response.OverallStars)
        .Add("comment", response => response.Comment, searchable: true)
        .Add("createdAt", response => response.CreatedAt)
        .DefaultOrder("createdAt", descending: true)
        .PageSize(fallback: 25, max: 200);

    private static readonly Expression<Func<RatingResponse, AdminRatingResponseSummary>> ToSummary =
        response => new AdminRatingResponseSummary(
            response.Id,
            response.RatingTypeId,
            response.Type!.Name,
            response.Type!.Code,
            response.UserId,
            response.TargetId,
            response.OverallStars,
            response.Comment,
            response.Answers.Count,
            response.Answers.Count == 0 ? null : response.Answers.Average(answer => (double)answer.Stars),
            response.IsActive,
            response.CreatedAt,
            response.UpdatedAt);

    public async Task<AdminRatingResponsesPage> ListResponsesAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        // Soft-deleted responses are out of this view entirely, headline average
        // included, so the predicate composes ahead of the grid's own filters.
        var responses = await dbContext.RatingResponses
            .Where(response => response.IsActive)
            .ToGridPageAsync(query, Columns, response => response.Id, ToSummary, cancellationToken);

        // The headline is the average over the whole FILTERED set, not over the
        // page, so it re-composes the same query with no Skip/Take — the split
        // between ApplyGrid and ToGridPageAsync exists for exactly this.
        var scored = dbContext.RatingResponses
            .Where(response => response.IsActive && response.OverallStars != null)
            .ApplyGrid(query, Columns, response => response.Id);

        var scoredCount = await scored.CountAsync(cancellationToken);
        var averageOverall = scoredCount == 0
            ? 0d
            : await scored.AverageAsync(
                response => (double)response.OverallStars!.Value, cancellationToken);

        return new AdminRatingResponsesPage(responses, averageOverall, responses.Total);
    }

    public async Task<AdminRatingKpiView> GetKpiAsync(CancellationToken cancellationToken = default)
    {
        var types = await dbContext.RatingTypes.AsNoTracking()
            .Where(type => type.IsActive)
            .OrderBy(type => type.DisplayOrder).ThenBy(type => type.Name)
            .Select(type => new { type.Id, type.Code, type.Name, type.NameArabic })
            .ToListAsync(cancellationToken);

        var responseTotalsByType = (await dbContext.RatingResponses.AsNoTracking()
            .Where(response => response.IsActive)
            .GroupBy(response => response.RatingTypeId)
            .Select(bucket => new
            {
                TypeId = bucket.Key,
                Count = bucket.Count(),
                OverallCount = bucket.Count(response => response.OverallStars != null),
                OverallSum = bucket.Sum(response => response.OverallStars ?? 0),
            })
            .ToListAsync(cancellationToken))
            .ToDictionary(totals => totals.TypeId);

        var questions = await dbContext.RatingQuestions.AsNoTracking()
            .Where(question => question.IsActive)
            .OrderBy(question => question.DisplayOrder).ThenBy(question => question.Text)
            .Select(question => new
            {
                question.Id, question.RatingTypeId, question.Text, question.TextArabic,
            })
            .ToListAsync(cancellationToken);

        var answerTotalsByQuestion = (await dbContext.RatingAnswers.AsNoTracking()
            .Where(answer => answer.Response!.IsActive)
            .GroupBy(answer => answer.RatingQuestionId)
            .Select(bucket => new
            {
                QuestionId = bucket.Key,
                Count = bucket.Count(),
                Sum = bucket.Sum(answer => answer.Stars),
            })
            .ToListAsync(cancellationToken))
            .ToDictionary(totals => totals.QuestionId);

        var typeKpis = types.Select(type =>
        {
            var responseTotals = responseTotalsByType.GetValueOrDefault(type.Id);
            var responseCount = responseTotals?.Count ?? 0;
            var averageOverall = responseTotals is { OverallCount: > 0 }
                ? (double)responseTotals.OverallSum / responseTotals.OverallCount
                : 0d;

            var questionKpis = questions
                .Where(question => question.RatingTypeId == type.Id)
                .Select(question =>
                {
                    var answerTotals = answerTotalsByQuestion.GetValueOrDefault(question.Id);
                    var count = answerTotals?.Count ?? 0;
                    var average = count > 0 ? (double)answerTotals!.Sum / count : 0d;
                    return new RatingQuestionKpi(
                        question.Id, question.Text, question.TextArabic, count, average);
                })
                .ToList();

            return new RatingTypeKpi(
                type.Id, type.Code, type.Name, type.NameArabic,
                responseCount, averageOverall, questionKpis);
        }).ToList();

        var totalResponses = await dbContext.RatingResponses
            .CountAsync(response => response.IsActive, cancellationToken);

        return new AdminRatingKpiView(totalResponses, typeKpis);
    }
}
