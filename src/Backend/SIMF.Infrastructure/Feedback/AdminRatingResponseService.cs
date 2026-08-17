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
            .Where(t => t.IsActive)
            .OrderBy(t => t.DisplayOrder).ThenBy(t => t.Name)
            .Select(t => new { t.Id, t.Code, t.Name, t.NameArabic })
            .ToListAsync(cancellationToken);

        var responseAgg = (await dbContext.RatingResponses.AsNoTracking()
            .Where(r => r.IsActive)
            .GroupBy(r => r.RatingTypeId)
            .Select(g => new
            {
                TypeId = g.Key,
                Count = g.Count(),
                OverallCount = g.Count(r => r.OverallStars != null),
                OverallSum = g.Sum(r => r.OverallStars ?? 0),
            })
            .ToListAsync(cancellationToken))
            .ToDictionary(x => x.TypeId);

        var questions = await dbContext.RatingQuestions.AsNoTracking()
            .Where(q => q.IsActive)
            .OrderBy(q => q.DisplayOrder).ThenBy(q => q.Text)
            .Select(q => new { q.Id, q.RatingTypeId, q.Text, q.TextArabic })
            .ToListAsync(cancellationToken);

        var answerAgg = (await dbContext.RatingAnswers.AsNoTracking()
            .Where(a => a.Response!.IsActive)
            .GroupBy(a => a.RatingQuestionId)
            .Select(g => new { QuestionId = g.Key, Count = g.Count(), Sum = g.Sum(a => a.Stars) })
            .ToListAsync(cancellationToken))
            .ToDictionary(x => x.QuestionId);

        var typeKpis = types.Select(t =>
        {
            var agg = responseAgg.GetValueOrDefault(t.Id);
            var responseCount = agg?.Count ?? 0;
            var averageOverall = agg is { OverallCount: > 0 }
                ? (double)agg.OverallSum / agg.OverallCount
                : 0d;

            var questionKpis = questions
                .Where(q => q.RatingTypeId == t.Id)
                .Select(q =>
                {
                    var qAgg = answerAgg.GetValueOrDefault(q.Id);
                    var count = qAgg?.Count ?? 0;
                    var average = count > 0 ? (double)qAgg!.Sum / count : 0d;
                    return new RatingQuestionKpi(q.Id, q.Text, q.TextArabic, count, average);
                })
                .ToList();

            return new RatingTypeKpi(
                t.Id, t.Code, t.Name, t.NameArabic, responseCount, averageOverall, questionKpis);
        }).ToList();

        var totalResponses = await dbContext.RatingResponses
            .CountAsync(r => r.IsActive, cancellationToken);

        return new AdminRatingKpiView(totalResponses, typeKpis);
    }
}
