// Tests: SIMF.Api.Tests/FeedbackRatingsTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Feedback.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Feedback;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Feedback;

/// <summary>Read-only admin view over submitted rating responses: the paged grid
/// (with the overall-average headline) and the per-type / per-question KPI
/// aggregates. Responses are owned by the attendees who submit them, so this is a
/// viewer only — no create/edit here.</summary>
internal sealed class AdminRatingResponseService(
    SimfAppDbContext dbContext) : IAdminRatingResponseService
{
    public async Task<AdminRatingResponsesPage> ListResponsesAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 25, 1, 200);

        var rows = dbContext.RatingResponses.AsNoTracking()
            .Where(r => r.IsActive);

        if (query.Filters.TryGetValue("ratingTypeId", out var typeFilter)
            && Guid.TryParse(typeFilter, out var ratingTypeId))
        {
            rows = rows.Where(r => r.RatingTypeId == ratingTypeId);
        }
        if (query.Filters.TryGetValue("comment", out var commentFilter)
            && !string.IsNullOrWhiteSpace(commentFilter))
        {
            var v = commentFilter.Trim();
            rows = rows.Where(r => r.Comment != null && r.Comment.Contains(v));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(r =>
                r.Comment != null && EF.Functions.Like(r.Comment, $"%{term}%"));
        }

        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("overall", true) => rows.OrderByDescending(r => r.OverallStars),
            ("overall", false) => rows.OrderBy(r => r.OverallStars),
            ("createdat", false) => rows.OrderBy(r => r.CreatedAt),
            _ => rows.OrderByDescending(r => r.CreatedAt),
        };

        var total = await rows.CountAsync(cancellationToken);

        // Average over the responses that carry an overall score; 0 when none.
        var overallCount = await rows.CountAsync(r => r.OverallStars != null, cancellationToken);
        var averageOverall = overallCount == 0
            ? 0d
            : await rows.Where(r => r.OverallStars != null)
                .AverageAsync(r => (double)r.OverallStars!.Value, cancellationToken);

        var page = await rows
            .Skip(skip)
            .Take(top)
            .Select(r => new AdminRatingResponseSummary(
                r.Id,
                r.RatingTypeId,
                r.Type!.Name,
                r.Type!.Code,
                r.UserId,
                r.TargetId,
                r.OverallStars,
                r.Comment,
                r.Answers.Count,
                r.Answers.Count == 0 ? null : r.Answers.Average(a => (double)a.Stars),
                r.IsActive,
                r.CreatedAt,
                r.UpdatedAt))
            .ToListAsync(cancellationToken);

        var grid = GridPage<AdminRatingResponseSummary>.Of(page, total,
            new GridQuery { Skip = skip, Top = top });
        return new AdminRatingResponsesPage(grid, averageOverall, total);
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
