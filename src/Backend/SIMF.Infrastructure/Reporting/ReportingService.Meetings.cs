// Tests: SIMF.Api.Tests/ReportingTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Excel;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Reporting;

namespace SIMF.Infrastructure.Reporting;

/// <summary>
/// Meetings report — speaker meeting requests and delegation meeting requests in
/// one list.
///
/// <para>They are separate tables with different targets (a speaker versus a
/// country) but the same operational shape: who asked, of whom, for when, how it
/// was answered, and whether they turned up. An organiser chasing unanswered
/// requests wants one list, not two.</para>
///
/// <para>Both are projected separately then concatenated in memory, for the same
/// reason as the partners report: no shared base type, and the volumes are
/// meeting requests rather than an audit log.</para>
/// </summary>
internal sealed partial class ReportingService
{
    public async Task<ReportPage<MeetingsReportRow>> GetMeetingsAsync(
        ReportQuery query, CancellationToken cancellationToken = default)
    {
        var (skip, top) = ResolvePaging(query);
        var all = await LoadMeetingsAsync(query, cancellationToken);

        var pending = all.Count(m => m.Status == nameof(MeetingRequestStatus.Pending));
        var checkedIn = all.Count(m => m.CheckedIn);

        return new ReportPage<MeetingsReportRow>(
            all.Skip(skip).Take(top).ToList(),
            all.Count,
            skip,
            top,
            [
                new ReportTotal("Admin.Reports.Total.Meetings", Figure(all.Count)),
                new ReportTotal("Admin.Reports.Total.PendingMeetings", Figure(pending)),
                new ReportTotal("Admin.Reports.Total.CheckedIn", Figure(checkedIn)),
            ]);
    }

    public async Task<byte[]> ExportMeetingsAsync(
        ReportQuery query, CancellationToken cancellationToken = default)
    {
        var all = await LoadMeetingsAsync(query, cancellationToken);

        return Export(
            all.Take(ExportRowCap).ToList(),
            [
                new GridExcelColumn<MeetingsReportRow>("Kind", r => r.Kind),
                new GridExcelColumn<MeetingsReportRow>("Requester", r => r.Requester),
                new GridExcelColumn<MeetingsReportRow>("Target", r => r.Target),
                new GridExcelColumn<MeetingsReportRow>("Subject", r => r.Subject),
                new GridExcelColumn<MeetingsReportRow>("Slot", r => r.SlotDisplay),
                new GridExcelColumn<MeetingsReportRow>("Status", r => r.Status),
                new GridExcelColumn<MeetingsReportRow>("Requested", r => r.RequestedDisplay),
                new GridExcelColumn<MeetingsReportRow>("Checked in", r => r.CheckedIn),
            ],
            "meetings");
    }

    private static class MeetingKind
    {
        public const string Speaker = "Speaker";
        public const string Delegation = "Delegation";
    }

    private async Task<List<MeetingsReportRow>> LoadMeetingsAsync(
        ReportQuery query, CancellationToken cancellationToken)
    {
        var window = ResolveWindow(query);
        var term = SearchTerm(query);

        // The range filters on when the request was MADE, not on its slot: an
        // organiser reviewing "what came in this week" means the request date.
        var speakerRequests = appDbContext.SpeakerMeetingRequests.AsNoTracking().AsQueryable();
        var delegationRequests = appDbContext.DelegationMeetingRequests.AsNoTracking().AsQueryable();

        if (window.Start is { } start)
        {
            speakerRequests = speakerRequests.Where(r => r.CreatedAt >= start);
            delegationRequests = delegationRequests.Where(r => r.CreatedAt >= start);
        }

        if (window.End is { } end)
        {
            speakerRequests = speakerRequests.Where(r => r.CreatedAt < end);
            delegationRequests = delegationRequests.Where(r => r.CreatedAt < end);
        }

        var speakers = await speakerRequests
            .Select(r => new MeetingProjection(
                r.Id,
                MeetingKind.Speaker,
                r.RequesterName,
                r.Speaker != null ? r.Speaker.Name : string.Empty,
                r.Subject,
                r.SlotStart,
                r.SlotEnd,
                r.Status,
                r.CreatedAt,
                r.CheckedInAt != null))
            .ToListAsync(cancellationToken);

        var delegations = await delegationRequests
            .Select(r => new MeetingProjection(
                r.Id,
                MeetingKind.Delegation,
                r.RequestingCountry != null ? r.RequestingCountry.Name : string.Empty,
                r.TargetCountry != null ? r.TargetCountry.Name : string.Empty,
                r.Subject,
                r.SlotStart,
                r.SlotEnd,
                r.Status,
                r.CreatedAt,
                r.CheckedInAt != null))
            .ToListAsync(cancellationToken);

        var all = speakers.Concat(delegations).Select(ToRow);

        if (term is not null)
        {
            all = all.Where(m =>
                m.Requester.Contains(term, StringComparison.OrdinalIgnoreCase)
                || m.Target.Contains(term, StringComparison.OrdinalIgnoreCase)
                || m.Subject.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var sorted = query.Grid.Sort switch
        {
            "status" => query.Grid.SortDescending
                ? all.OrderByDescending(m => m.Status, StringComparer.Ordinal)
                : all.OrderBy(m => m.Status, StringComparer.Ordinal),
            "kind" => query.Grid.SortDescending
                ? all.OrderByDescending(m => m.Kind, StringComparer.Ordinal)
                : all.OrderBy(m => m.Kind, StringComparer.Ordinal),
            // Newest request first.
            _ => query.Grid.SortDescending
                ? all.OrderBy(m => m.RequestedDisplay, StringComparer.Ordinal)
                : all.OrderByDescending(m => m.RequestedDisplay, StringComparer.Ordinal),
        };

        return sorted.ThenBy(m => m.Id).ToList();
    }

    private sealed record MeetingProjection(
        Guid Id,
        string Kind,
        string Requester,
        string Target,
        string Subject,
        DateTime? SlotStart,
        DateTime? SlotEnd,
        MeetingRequestStatus Status,
        DateTime CreatedAt,
        bool CheckedIn);

    private static MeetingsReportRow ToRow(MeetingProjection p) =>
        new(
            p.Id,
            p.Kind,
            p.Requester,
            p.Target,
            p.Subject,
            // A request can exist before a slot is agreed, so an unscheduled
            // meeting shows blank rather than a fabricated time.
            p.SlotStart is { } slot
                ? slot.FormatSaudi() + (p.SlotEnd is { } endsAt
                    ? " - " + endsAt.FormatSaudiTime()
                    : string.Empty)
                : string.Empty,
            p.Status.ToString(),
            p.CreatedAt.FormatSaudi(),
            p.CheckedIn);
}
