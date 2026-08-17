// Tests: SIMF.Api.Tests/ReportingTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Excel;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Reporting;
using SIMF.Domain.SessionQuestions;

namespace SIMF.Infrastructure.Reporting;

/// <summary>
/// Engagement report — the audience questions asked in sessions, with their
/// moderation state.
///
/// <para>Hidden questions are INCLUDED. This is a moderation report: an
/// organiser reviewing what was asked needs to see what was suppressed, which is
/// exactly the part a public-facing query filters out. The Hidden column makes
/// the state explicit rather than silently omitting the row.</para>
///
/// <para>Hidden is read off Status, the single source of truth for visibility,
/// and not off the stored IsHidden column, which no writer has set since the
/// moderation desk moved visibility onto Status: counting the column reported
/// zero hidden questions on every run and marked genuinely hidden rows false in
/// the export.</para>
///
/// <para>The asker is not reported. Attaching a name to a question would turn
/// an audience channel into an attributed one; the moderation decision is about
/// the question, not the person.</para>
/// </summary>
internal sealed partial class ReportingService
{
    public async Task<ReportPage<EngagementReportRow>> GetEngagementAsync(
        ReportQuery query, CancellationToken cancellationToken = default)
    {
        var (skip, top) = ResolvePaging(query);
        var filtered = FilterQuestions(query);

        var total = await filtered.CountAsync(cancellationToken);

        var page = await SortQuestions(filtered, query)
            .Skip(skip)
            .Take(top)
            .Select(q => new QuestionProjection(
                q.Id,
                q.Session != null ? q.Session.Code : string.Empty,
                q.Session != null ? q.Session.Title : string.Empty,
                q.QuestionText,
                q.Recipient,
                q.Status,
                q.Phase,
                q.Status == QuestionStatus.Hidden,
                q.CreatedAt))
            .ToListAsync(cancellationToken);

        var hidden = await filtered.CountAsync(
            q => q.Status == QuestionStatus.Hidden, cancellationToken);
        var pushed = await filtered.CountAsync(q => q.IsPushed, cancellationToken);

        return new ReportPage<EngagementReportRow>(
            page.Select(ToRow).ToList(),
            total,
            skip,
            top,
            [
                new ReportTotal("Admin.Reports.Total.Questions", Figure(total)),
                new ReportTotal("Admin.Reports.Total.HiddenQuestions", Figure(hidden)),
                new ReportTotal("Admin.Reports.Total.PushedQuestions", Figure(pushed)),
            ]);
    }

    public async Task<byte[]> ExportEngagementAsync(
        ReportQuery query, CancellationToken cancellationToken = default)
    {
        var rows = await SortQuestions(FilterQuestions(query), query)
            .Take(ExportRowCap)
            .Select(q => new QuestionProjection(
                q.Id,
                q.Session != null ? q.Session.Code : string.Empty,
                q.Session != null ? q.Session.Title : string.Empty,
                q.QuestionText,
                q.Recipient,
                q.Status,
                q.Phase,
                q.Status == QuestionStatus.Hidden,
                q.CreatedAt))
            .ToListAsync(cancellationToken);

        return Export(
            rows.Select(ToRow).ToList(),
            [
                new GridExcelColumn<EngagementReportRow>("Session code", r => r.SessionCode),
                new GridExcelColumn<EngagementReportRow>("Session", r => r.SessionTitle),
                new GridExcelColumn<EngagementReportRow>("Question", r => r.QuestionText),
                new GridExcelColumn<EngagementReportRow>("Recipient", r => r.Recipient),
                new GridExcelColumn<EngagementReportRow>("Status", r => r.Status),
                new GridExcelColumn<EngagementReportRow>("Phase", r => r.Phase),
                new GridExcelColumn<EngagementReportRow>("Hidden", r => r.IsHidden),
                new GridExcelColumn<EngagementReportRow>("Asked", r => r.AskedDisplay),
            ],
            "engagement");
    }

    private sealed record QuestionProjection(
        Guid Id,
        string SessionCode,
        string SessionTitle,
        string QuestionText,
        SessionQuestionRecipient Recipient,
        QuestionStatus Status,
        QuestionPhase Phase,
        bool IsHidden,
        DateTime CreatedAt);

    private static EngagementReportRow ToRow(QuestionProjection p) =>
        new(
            p.Id,
            p.SessionCode,
            p.SessionTitle,
            p.QuestionText,
            p.Recipient.ToString(),
            p.Status.ToString(),
            p.Phase.ToString(),
            p.IsHidden,
            p.CreatedAt.FormatSaudi());

    private IQueryable<SessionQuestion> FilterQuestions(ReportQuery query)
    {
        var window = ResolveWindow(query);
        var questions = appDbContext.SessionQuestions.AsNoTracking();

        if (window.Start is { } start)
        {
            questions = questions.Where(q => q.CreatedAt >= start);
        }

        if (window.End is { } end)
        {
            questions = questions.Where(q => q.CreatedAt < end);
        }

        if (SearchTerm(query) is { } term)
        {
            questions = questions.Where(q =>
                q.QuestionText.Contains(term)
                || (q.Session != null && q.Session.Title.Contains(term))
                || (q.Session != null && q.Session.Code.Contains(term)));
        }

        return questions;
    }

    private static IQueryable<SessionQuestion> SortQuestions(
        IQueryable<SessionQuestion> questions, ReportQuery query)
    {
        var descending = query.Grid.SortDescending;

        return query.Grid.Sort switch
        {
            "session" => descending
                ? questions.OrderByDescending(q => q.Session!.Title).ThenBy(q => q.Id)
                : questions.OrderBy(q => q.Session!.Title).ThenBy(q => q.Id),
            "status" => descending
                ? questions.OrderByDescending(q => q.Status).ThenBy(q => q.Id)
                : questions.OrderBy(q => q.Status).ThenBy(q => q.Id),
            // "asked" is the date column's own Key (EngagementReport.razor:71).
            // Without this arm a click fell through to the default below, which
            // reads `descending` INVERTED so the no-sort case comes back
            // newest-first — leaving the ascending arrow (and
            // aria-sort="ascending") sitting over newest-first rows.
            "asked" => descending
                ? questions.OrderByDescending(q => q.CreatedAt).ThenBy(q => q.Id)
                : questions.OrderBy(q => q.CreatedAt).ThenBy(q => q.Id),
            // Newest first by default: the queue is read from the live end.
            _ => descending
                ? questions.OrderBy(q => q.CreatedAt).ThenBy(q => q.Id)
                : questions.OrderByDescending(q => q.CreatedAt).ThenBy(q => q.Id),
        };
    }
}
