// Tests: SIMF.Api.Tests/ReportingTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Excel;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Reporting;
using SIMF.Domain.AccessControl;

namespace SIMF.Infrastructure.Reporting;

/// <summary>
/// Gate-activity report — one row per recorded scan, allowed or denied.
///
/// <para>The visitor name and profile type come from the scan's own
/// <c>ScannedDisplayName</c> / <c>ScannedProfileTypeName</c> snapshots rather
/// than from a lookup. <c>GateScan</c> is an append-only audit log and those
/// columns exist precisely so a historic scan still reads correctly after the
/// linked account is renamed or removed (D-157). Resolving them live would make
/// the report disagree with the audit trail it is reporting on.</para>
/// </summary>
internal sealed partial class ReportingService
{
    public async Task<ReportPage<GateActivityReportRow>> GetGateActivityAsync(
        ReportQuery query, CancellationToken cancellationToken = default)
    {
        var (skip, top) = ResolvePaging(query);
        var filtered = FilterScans(query);

        var total = await filtered.CountAsync(cancellationToken);

        var page = await SortScans(filtered, query)
            .Skip(skip)
            .Take(top)
            .Select(s => new ScanProjection(
                s.Id,
                s.Gate != null ? s.Gate.Name : string.Empty,
                s.ScannedAt,
                s.Direction,
                s.Outcome,
                s.DenialReasonCode,
                s.ScannedDisplayName,
                s.ScannedProfileTypeName))
            .ToListAsync(cancellationToken);

        var allowed = await filtered
            .CountAsync(s => s.Outcome == ScanOutcome.Allowed, cancellationToken);
        var denied = await filtered
            .CountAsync(s => s.Outcome == ScanOutcome.Denied, cancellationToken);

        // Distinct people admitted, which is the figure operations actually want
        // from a gate log: a scan count double-counts anyone who re-enters.
        var distinctAdmitted = await filtered
            .Where(s => s.Outcome == ScanOutcome.Allowed
                && s.Direction == ScanDirection.CheckIn
                && s.UserProfileId != null)
            .Select(s => s.UserProfileId!.Value)
            .Distinct()
            .CountAsync(cancellationToken);

        return new ReportPage<GateActivityReportRow>(
            page.Select(ToRow).ToList(),
            total,
            skip,
            top,
            [
                new ReportTotal("Admin.Reports.Total.Scans", Figure(total)),
                new ReportTotal("Admin.Reports.Total.Allowed", Figure(allowed)),
                new ReportTotal("Admin.Reports.Total.Denied", Figure(denied)),
                new ReportTotal("Admin.Reports.Total.DistinctAdmitted", Figure(distinctAdmitted)),
            ]);
    }

    public async Task<byte[]> ExportGateActivityAsync(
        ReportQuery query, CancellationToken cancellationToken = default)
    {
        var rows = await SortScans(FilterScans(query), query)
            .Take(ExportRowCap)
            .Select(s => new ScanProjection(
                s.Id,
                s.Gate != null ? s.Gate.Name : string.Empty,
                s.ScannedAt,
                s.Direction,
                s.Outcome,
                s.DenialReasonCode,
                s.ScannedDisplayName,
                s.ScannedProfileTypeName))
            .ToListAsync(cancellationToken);

        return Export(
            rows.Select(ToRow).ToList(),
            [
                new GridExcelColumn<GateActivityReportRow>("Gate", r => r.GateName),
                new GridExcelColumn<GateActivityReportRow>("Scanned", r => r.ScannedDisplay),
                new GridExcelColumn<GateActivityReportRow>("Direction", r => r.Direction),
                new GridExcelColumn<GateActivityReportRow>("Outcome", r => r.Outcome),
                new GridExcelColumn<GateActivityReportRow>("Denial reason", r => r.DenialReason),
                new GridExcelColumn<GateActivityReportRow>("Visitor", r => r.VisitorName),
                new GridExcelColumn<GateActivityReportRow>("Profile type", r => r.ProfileTypeName),
            ],
            "gate-activity");
    }

    private sealed record ScanProjection(
        long Id,
        string GateName,
        DateTime ScannedAt,
        ScanDirection Direction,
        ScanOutcome Outcome,
        DenialReasonCode? DenialReason,
        string? VisitorName,
        string? ProfileTypeName);

    private static GateActivityReportRow ToRow(ScanProjection p) =>
        new(
            p.Id,
            p.GateName,
            p.ScannedAt.FormatSaudi(),
            p.Direction.ToString(),
            p.Outcome.ToString(),
            p.DenialReason?.ToString(),
            p.VisitorName,
            p.ProfileTypeName);

    /// <summary>
    /// Scans inside the requested Saudi window. <c>ScannedAt</c> is the
    /// authoritative server clock; the device-asserted <c>ClientScannedAt</c> is
    /// deliberately not used for filtering.
    /// </summary>
    private IQueryable<GateScan> FilterScans(ReportQuery query)
    {
        var window = ResolveWindow(query);
        var scans = appDbContext.GateScans.AsNoTracking();

        if (window.Start is { } start)
        {
            scans = scans.Where(s => s.ScannedAt >= start);
        }

        if (window.End is { } end)
        {
            scans = scans.Where(s => s.ScannedAt < end);
        }

        if (SearchTerm(query) is { } term)
        {
            scans = scans.Where(s =>
                (s.Gate != null && s.Gate.Name.Contains(term))
                || (s.ScannedDisplayName != null && s.ScannedDisplayName.Contains(term))
                || (s.ScannedProfileTypeName != null && s.ScannedProfileTypeName.Contains(term)));
        }

        return scans;
    }

    private static IQueryable<GateScan> SortScans(
        IQueryable<GateScan> scans, ReportQuery query)
    {
        var descending = query.Grid.SortDescending;

        return query.Grid.Sort switch
        {
            "gate" => descending
                ? scans.OrderByDescending(s => s.Gate!.Name).ThenBy(s => s.Id)
                : scans.OrderBy(s => s.Gate!.Name).ThenBy(s => s.Id),
            "outcome" => descending
                ? scans.OrderByDescending(s => s.Outcome).ThenBy(s => s.Id)
                : scans.OrderBy(s => s.Outcome).ThenBy(s => s.Id),
            "visitor" => descending
                ? scans.OrderByDescending(s => s.ScannedDisplayName).ThenBy(s => s.Id)
                : scans.OrderBy(s => s.ScannedDisplayName).ThenBy(s => s.Id),
            // Most recent first: a gate log is read from the live end.
            _ => descending
                ? scans.OrderBy(s => s.ScannedAt).ThenBy(s => s.Id)
                : scans.OrderByDescending(s => s.ScannedAt).ThenBy(s => s.Id),
        };
    }
}
