// Tests: SIMF.Api.Tests/ReportingTests.cs
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Excel;
using SIMF.Common;
using SIMF.Contracts.Reporting;
using SIMF.Domain.Feedback;

namespace SIMF.Infrastructure.Reporting;

/// <summary>
/// Ratings report — one row per submitted rating, filtered by the Saudi day it
/// was submitted.
///
/// <para><c>TargetId</c> is passed through as an opaque id. What it points at
/// depends on the rating type's scope (a session, a programme day, the forum),
/// so resolving it to a name would mean branching to a different table per row.
/// The type name and scope tell the reader what the id refers to.</para>
///
/// <para>The respondent is deliberately NOT reported. A rating carries a free-text
/// comment, and attaching a name to it would turn an anonymous feedback channel
/// into an attributed one.</para>
/// </summary>
internal sealed partial class ReportingService
{
    public async Task<ReportPage<RatingsReportRow>> GetRatingsAsync(
        ReportQuery query, CancellationToken cancellationToken = default)
    {
        var (skip, top) = ResolvePaging(query);
        var filtered = FilterRatings(query);

        var total = await filtered.CountAsync(cancellationToken);

        var page = await SortRatings(filtered, query)
            .Skip(skip)
            .Take(top)
            .Select(r => new RatingsProjection(
                r.Id,
                r.Type != null ? r.Type.Name : string.Empty,
                r.Type != null ? r.Type.Scope.ToString() : string.Empty,
                r.TargetId,
                r.OverallStars,
                r.Comment,
                r.CreatedAt))
            .ToListAsync(cancellationToken);

        var scored = filtered.Where(r => r.OverallStars != null);
        var average = await scored
            .Select(r => (double?)r.OverallStars!.Value)
            .AverageAsync(cancellationToken);
        var withComment = await filtered
            .CountAsync(r => r.Comment != null && r.Comment != string.Empty, cancellationToken);

        return new ReportPage<RatingsReportRow>(
            page.Select(ToRow).ToList(),
            total,
            skip,
            top,
            [
                new ReportTotal("Admin.Reports.Total.Ratings", Figure(total)),
                new ReportTotal(
                    "Admin.Reports.Total.AverageRating",
                    average is { } value
                        ? value.ToString("0.0", CultureInfo.InvariantCulture)
                        : string.Empty),
                new ReportTotal("Admin.Reports.Total.WithComment", Figure(withComment)),
            ]);
    }

    public async Task<byte[]> ExportRatingsAsync(
        ReportQuery query, CancellationToken cancellationToken = default)
    {
        var rows = await SortRatings(FilterRatings(query), query)
            .Take(ExportRowCap)
            .Select(r => new RatingsProjection(
                r.Id,
                r.Type != null ? r.Type.Name : string.Empty,
                r.Type != null ? r.Type.Scope.ToString() : string.Empty,
                r.TargetId,
                r.OverallStars,
                r.Comment,
                r.CreatedAt))
            .ToListAsync(cancellationToken);

        return Export(
            rows.Select(ToRow).ToList(),
            [
                new GridExcelColumn<RatingsReportRow>("Rating type", r => r.RatingTypeName),
                new GridExcelColumn<RatingsReportRow>("Scope", r => r.Scope),
                new GridExcelColumn<RatingsReportRow>("Target", r => r.TargetId.ToString()),
                new GridExcelColumn<RatingsReportRow>("Stars", r => r.OverallStars),
                new GridExcelColumn<RatingsReportRow>("Comment", r => r.Comment),
                new GridExcelColumn<RatingsReportRow>("Submitted", r => r.SubmittedDisplay),
            ],
            "ratings");
    }

    private sealed record RatingsProjection(
        Guid Id,
        string RatingTypeName,
        string Scope,
        Guid TargetId,
        int? OverallStars,
        string? Comment,
        DateTime CreatedAt);

    private static RatingsReportRow ToRow(RatingsProjection p) =>
        new(
            p.Id,
            p.RatingTypeName,
            p.Scope,
            p.TargetId,
            p.OverallStars,
            p.Comment,
            p.CreatedAt.FormatSaudi());

    private IQueryable<RatingResponse> FilterRatings(ReportQuery query)
    {
        var window = ResolveWindow(query);
        var ratings = appDbContext.RatingResponses.AsNoTracking().Where(r => r.IsActive);

        if (window.Start is { } start)
        {
            ratings = ratings.Where(r => r.CreatedAt >= start);
        }

        if (window.End is { } end)
        {
            ratings = ratings.Where(r => r.CreatedAt < end);
        }

        if (SearchTerm(query) is { } term)
        {
            ratings = ratings.Where(r =>
                (r.Comment != null && r.Comment.Contains(term))
                || (r.Type != null && r.Type.Name.Contains(term)));
        }

        return ratings;
    }

    private static IQueryable<RatingResponse> SortRatings(
        IQueryable<RatingResponse> ratings, ReportQuery query)
    {
        var descending = query.Grid.SortDescending;

        return query.Grid.Sort switch
        {
            "type" => descending
                ? ratings.OrderByDescending(r => r.Type!.Name).ThenBy(r => r.Id)
                : ratings.OrderBy(r => r.Type!.Name).ThenBy(r => r.Id),
            "stars" => descending
                ? ratings.OrderByDescending(r => r.OverallStars).ThenBy(r => r.Id)
                : ratings.OrderBy(r => r.OverallStars).ThenBy(r => r.Id),
            // Newest first: feedback is read from the live end.
            _ => descending
                ? ratings.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
                : ratings.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id),
        };
    }
}
