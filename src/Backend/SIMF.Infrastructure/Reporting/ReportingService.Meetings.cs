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

        // Sorted and searched over the PROJECTION, not over the display rows.
        // The row's "requested" cell is a day-first 12-hour string, so ordering
        // it compares day-of-month before month and "11:00 AM" above "01:00 PM";
        // the projection still carries the raw instant, which is the only thing
        // that orders correctly. Status and kind are the same values either way.
        var all = speakers.Concat(delegations);

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
                ? all.OrderByDescending(m => m.Status.ToString(), StringComparer.Ordinal)
                : all.OrderBy(m => m.Status.ToString(), StringComparer.Ordinal),
            "kind" => query.Grid.SortDescending
                ? all.OrderByDescending(m => m.Kind, StringComparer.Ordinal)
                : all.OrderBy(m => m.Kind, StringComparer.Ordinal),
            // "requested" is the request-date column's own Key (MeetingsReport
            // .razor). That column is not Sortable, so the Control Panel draws no
            // arrow on it and never sends this key today; the arm exists because
            // the endpoint takes whatever key it is given, and a date sort has to
            // mean what it says for whoever sends it. Descending is newest first,
            // literally, with none of the inversion the default below used to
            // hand down.
            "requested" => query.Grid.SortDescending
                ? all.OrderByDescending(m => m.CreatedAt)
                : all.OrderBy(m => m.CreatedAt),
            // No column chosen: the grid renders no arrow, so the direction flag
            // has nothing to point at and reading it here is what made the flag
            // mean its own opposite — an unsorted request asking for ascending
            // and being served newest-first. A meetings list is worked from the
            // newest request down, so that order is now stated outright instead
            // of arrived at by inverting a flag nobody set.
            _ => all.OrderByDescending(m => m.CreatedAt),
        };

        return sorted.ThenBy(m => m.Id).Select(ToRow).ToList();
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
