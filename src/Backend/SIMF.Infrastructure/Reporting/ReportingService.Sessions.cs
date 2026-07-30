// Tests: SIMF.Api.Tests/ReportingTests.cs
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Excel;
using SIMF.Common;
using SIMF.Contracts.Reporting;

namespace SIMF.Infrastructure.Reporting;

/// <summary>
/// Sessions report — the programme with its speakers, attendance, audience
/// questions and average score on one row.
///
/// <para>Where the attendance report answers "was this session attended", this
/// one answers "how did this session do": it adds the speaker list, the question
/// count and the rating.</para>
/// </summary>
internal sealed partial class ReportingService
{
    public async Task<ReportPage<SessionsReportRow>> GetSessionsAsync(
        ReportQuery query, CancellationToken cancellationToken = default)
    {
        var (skip, top) = ResolvePaging(query);
        var filtered = FilterSessions(query);

        var total = await filtered.CountAsync(cancellationToken);

        var page = await SortSessions(filtered, query)
            .Skip(skip)
            .Take(top)
            .Select(s => new SessionsProjection(
                s.Id,
                s.Code,
                s.Title,
                s.Hall != null ? s.Hall.Name : string.Empty,
                s.Start,
                // Ordered so the primary speaker leads, matching the agenda.
                s.Speakers
                    .OrderBy(ss => ss.DisplayOrder)
                    .Select(ss => ss.Speaker!.Name)
                    .ToList(),
                appDbContext.HallAttendances
                    .Where(a => a.SessionId == s.Id)
                    .Select(a => a.UserId)
                    .Distinct()
                    .Count(),
                appDbContext.SessionQuestions.Count(q => q.SessionId == s.Id),
                // Ratings target the session by its id; the nullable cast makes
                // an unrated session come back null rather than throwing.
                appDbContext.RatingResponses
                    .Where(r => r.IsActive && r.TargetId == s.Id && r.OverallStars != null)
                    .Average(r => (double?)r.OverallStars!.Value)))
            .ToListAsync(cancellationToken);

        var questionTotal = await appDbContext.SessionQuestions.AsNoTracking()
            .CountAsync(q => filtered.Select(s => s.Id).Contains(q.SessionId), cancellationToken);

        return new ReportPage<SessionsReportRow>(
            page.Select(ToRow).ToList(),
            total,
            skip,
            top,
            [
                new ReportTotal("Admin.Reports.Total.Sessions", Figure(total)),
                new ReportTotal("Admin.Reports.Total.Questions", Figure(questionTotal)),
            ]);
    }

    public async Task<byte[]> ExportSessionsAsync(
        ReportQuery query, CancellationToken cancellationToken = default)
    {
        var rows = await SortSessions(FilterSessions(query), query)
            .Take(ExportRowCap)
            .Select(s => new SessionsProjection(
                s.Id,
                s.Code,
                s.Title,
                s.Hall != null ? s.Hall.Name : string.Empty,
                s.Start,
                s.Speakers
                    .OrderBy(ss => ss.DisplayOrder)
                    .Select(ss => ss.Speaker!.Name)
                    .ToList(),
                appDbContext.HallAttendances
                    .Where(a => a.SessionId == s.Id)
                    .Select(a => a.UserId)
                    .Distinct()
                    .Count(),
                appDbContext.SessionQuestions.Count(q => q.SessionId == s.Id),
                appDbContext.RatingResponses
                    .Where(r => r.IsActive && r.TargetId == s.Id && r.OverallStars != null)
                    .Average(r => (double?)r.OverallStars!.Value)))
            .ToListAsync(cancellationToken);

        return Export(
            rows.Select(ToRow).ToList(),
            [
                new GridExcelColumn<SessionsReportRow>("Code", r => r.Code),
                new GridExcelColumn<SessionsReportRow>("Session", r => r.Title),
                new GridExcelColumn<SessionsReportRow>("Hall", r => r.HallName),
                new GridExcelColumn<SessionsReportRow>("Start", r => r.StartDisplay),
                new GridExcelColumn<SessionsReportRow>("Speakers", r => r.Speakers),
                new GridExcelColumn<SessionsReportRow>("Attendees", r => r.Attendees),
                new GridExcelColumn<SessionsReportRow>("Questions", r => r.Questions),
                new GridExcelColumn<SessionsReportRow>("Average rating", r => r.AverageRating),
            ],
            "sessions");
    }

    private sealed record SessionsProjection(
        Guid Id,
        string Code,
        string Title,
        string HallName,
        DateTimeOffset Start,
        List<string> Speakers,
        int Attendees,
        int Questions,
        double? AverageRating);

    private static SessionsReportRow ToRow(SessionsProjection p) =>
        new(
            p.Id,
            p.Code,
            p.Title,
            p.HallName,
            p.Start.FormatSaudi(),
            string.Join(", ", p.Speakers),
            p.Attendees,
            p.Questions,
            // Blank, not "0.0": an unrated session has no score, and printing a
            // zero would read as a unanimously terrible one.
            p.AverageRating is { } average
                ? average.ToString("0.0", CultureInfo.InvariantCulture)
                : string.Empty);
}
