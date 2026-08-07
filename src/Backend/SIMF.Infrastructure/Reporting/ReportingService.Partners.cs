// Tests: SIMF.Api.Tests/ReportingTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Excel;
using SIMF.Contracts.Reporting;

namespace SIMF.Infrastructure.Reporting;

/// <summary>
/// Partners report — the exhibitor, sponsor and booth lists flattened into one
/// contact directory, so an organiser can read the whole partner surface at once
/// instead of visiting three pages.
///
/// <para>The three tables share no base type and have genuinely different
/// columns, so they are projected separately and concatenated <b>in memory</b>.
/// That is deliberate: a UNION in SQL would need matching column lists and would
/// still not give a stable cross-table sort, and the partner lists are tens to
/// low hundreds of rows, not an audit log. If the exhibition ever grew to
/// thousands of partners this should become a real UNION with server paging.</para>
///
/// <para>This report ignores the date range. A partner directory is a snapshot of
/// who is participating, not a record of events in a period, so filtering it by
/// creation date would answer a question nobody asks.</para>
/// </summary>
internal sealed partial class ReportingService
{
    public async Task<ReportPage<PartnersReportRow>> GetPartnersAsync(
        ReportQuery query, CancellationToken cancellationToken = default)
    {
        var (skip, top) = ResolvePaging(query);
        var all = await LoadPartnersAsync(query, cancellationToken);

        var exhibitors = all.Count(p => p.Kind == PartnerKind.Exhibitor);
        var sponsors = all.Count(p => p.Kind == PartnerKind.Sponsor);
        var booths = all.Count(p => p.Kind == PartnerKind.Booth);

        return new ReportPage<PartnersReportRow>(
            all.Skip(skip).Take(top).ToList(),
            all.Count,
            skip,
            top,
            [
                new ReportTotal("Admin.Reports.Total.Partners", Figure(all.Count)),
                new ReportTotal("Admin.Reports.Total.Exhibitors", Figure(exhibitors)),
                new ReportTotal("Admin.Reports.Total.Sponsors", Figure(sponsors)),
                new ReportTotal("Admin.Reports.Total.Booths", Figure(booths)),
            ]);
    }

    public async Task<byte[]> ExportPartnersAsync(
        ReportQuery query, CancellationToken cancellationToken = default)
    {
        var all = await LoadPartnersAsync(query, cancellationToken);

        return Export(
            all.Take(ExportRowCap).ToList(),
            [
                new GridExcelColumn<PartnersReportRow>("Kind", r => r.Kind),
                new GridExcelColumn<PartnersReportRow>("Name", r => r.Name),
                new GridExcelColumn<PartnersReportRow>("Name (Arabic)", r => r.NameArabic),
                new GridExcelColumn<PartnersReportRow>("Tier", r => r.Tier),
                new GridExcelColumn<PartnersReportRow>("Email", r => r.ContactEmail),
                new GridExcelColumn<PartnersReportRow>("Phone", r => r.ContactPhone),
                new GridExcelColumn<PartnersReportRow>("Website", r => r.Website),
                new GridExcelColumn<PartnersReportRow>("Active", r => r.IsActive),
            ],
            "partners");
    }

    /// <summary>The literal kind labels. Constants rather than inline strings so
    /// the counts and the rows cannot drift apart.</summary>
    private static class PartnerKind
    {
        public const string Exhibitor = "Exhibitor";
        public const string Sponsor = "Sponsor";
        public const string Booth = "Booth";
    }

    private async Task<List<PartnersReportRow>> LoadPartnersAsync(
        ReportQuery query, CancellationToken cancellationToken)
    {
        var term = SearchTerm(query);

        var exhibitors = await appDbContext.Exhibitors.AsNoTracking()
            .Where(e => e.IsActive)
            .Select(e => new PartnersReportRow(
                e.Id, PartnerKind.Exhibitor, e.Name, e.NameArabic,
                e.Tier != null ? e.Tier.ToString() : null,
                e.ContactEmail, e.ContactPhone, e.Website, e.IsActive))
            .ToListAsync(cancellationToken);

        var sponsors = await appDbContext.Sponsors.AsNoTracking()
            .Where(s => s.IsActive)
            .Select(s => new PartnersReportRow(
                s.Id, PartnerKind.Sponsor, s.Name, s.NameArabic,
                s.Tier.ToString(),
                s.Email, s.PhonePrimary, s.Url, s.IsActive))
            .ToListAsync(cancellationToken);

        var booths = await appDbContext.Booths.AsNoTracking()
            .Where(b => b.IsActive)
            .Select(b => new PartnersReportRow(
                b.Id, PartnerKind.Booth, b.Name, b.NameArabic,
                b.Sector,
                b.OfficerEmail, b.OfficerPhone, b.OfficerWebsite, b.IsActive))
            .ToListAsync(cancellationToken);

        var all = exhibitors.Concat(sponsors).Concat(booths);

        if (term is not null)
        {
            all = all.Where(p =>
                p.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || p.NameArabic.Contains(term, StringComparison.Ordinal)
                || (p.ContactEmail?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        // Kind then name, so the three lists stay grouped and readable. The id
        // tiebreak keeps paging stable when two partners share a name.
        var sorted = query.Grid.Sort switch
        {
            "name" => query.Grid.SortDescending
                ? all.OrderByDescending(p => p.Name, StringComparer.OrdinalIgnoreCase)
                : all.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase),
            _ => query.Grid.SortDescending
                ? all.OrderByDescending(p => p.Kind, StringComparer.Ordinal)
                    .ThenByDescending(p => p.Name, StringComparer.OrdinalIgnoreCase)
                : all.OrderBy(p => p.Kind, StringComparer.Ordinal)
                    .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase),
        };

        return sorted.ThenBy(p => p.Id).ToList();
    }
}
