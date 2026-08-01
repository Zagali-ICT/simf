// Tests: SIMF.Api.Tests/VipRosterTests.cs
using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Infrastructure.Excel;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// V-1 (D-429) — builds the VVIP/VIP welcome roster (موج) and renders it as CSV
/// or Excel. Cross-DB read (D-157): VVIP/VIP profiles come from
/// <see cref="SimfAppDbContext"/>; the owners' email / display-name / state are
/// resolved in one batched query against <see cref="SimfIdentityDbContext"/>.
/// </summary>
internal sealed class VipRosterService(
    SimfIdentityDbContext dbContext,
    SimfAppDbContext appDbContext)
    : IVipRosterService
{
    // The audience-side tiers the roster covers. Matches the seeded names.
    private static readonly string[] TierNames = { "VVIP", "VIP" };

    // Column order used by both the CSV and the Excel renderers + the page.
    private static readonly string[] Headers =
    {
        "Mawj ID", "Honorific", "Tier", "English name", "Arabic name",
        "Display name", "Job title", "Job title (Arabic)", "Preferred language", "Email", "Mobile",
        "Reference", "State", "Has photo", "Registered",
    };

    public async Task<IReadOnlyList<VipRosterRow>> GetRosterAsync(
        CancellationToken cancellationToken = default)
    {
        var profiles = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.ProfileType != null
                && p.ProfileType.IsForVisitor
                && TierNames.Contains(p.ProfileType.Name))
            .Select(p => new
            {
                p.UserId,
                p.Name,
                p.NameArabic,
                p.Honorific,
                p.JobTitle,
                p.JobTitleArabic,
                p.MawjId,
                p.PreferredLanguage,
                TierName = p.ProfileType!.Name,
                TierNameArabic = p.ProfileType.NameArabic,
                p.SaudiMobile,
                p.InternationalMobile,
                p.ReferenceNumber,
                HasVipPhoto = p.VipPhotoRelativePath != null,
                p.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        if (profiles.Count == 0)
        {
            return Array.Empty<VipRosterRow>();
        }

        var ids = profiles.Select(p => p.UserId).ToList();
        var users = await dbContext.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.Email, u.DisplayName, u.AccountState })
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        return profiles
            // VVIP before VIP, then alphabetical by English name.
            .OrderBy(p => p.TierName == "VVIP" ? 0 : 1)
            .ThenBy(p => p.Name)
            .Select(p =>
            {
                users.TryGetValue(p.UserId, out var user);
                return new VipRosterRow(
                    p.UserId,
                    user?.DisplayName ?? string.Empty,
                    p.Name,
                    p.NameArabic,
                    p.Honorific,
                    p.JobTitle,
                    p.JobTitleArabic,
                    p.MawjId,
                    p.PreferredLanguage,
                    p.TierName,
                    p.TierNameArabic,
                    user?.Email ?? string.Empty,
                    string.IsNullOrWhiteSpace(p.SaudiMobile) ? p.InternationalMobile : p.SaudiMobile,
                    p.ReferenceNumber,
                    (user?.AccountState ?? AccountState.PendingApproval).ToString(),
                    p.HasVipPhoto,
                    p.CreatedAt);
            })
            .ToList();
    }

    public async Task<GridPage<VipRosterRow>> GetRosterPageAsync(
        GridQuery query,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<VipRosterRow> rows = await GetRosterAsync(cancellationToken);

        // Free-text search across the columns a desk would scan.
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(r =>
                Contains(r.EnglishName, term) || Contains(r.ArabicName, term)
                || Contains(r.DisplayName, term) || Contains(r.Email, term)
                || Contains(r.MawjId, term) || Contains(r.Honorific, term)
                || Contains(r.ReferenceNumber, term));
        }

        // Per-column tier filter (the grid's only filterable column).
        if (query.Filters.TryGetValue("tier", out var tier) && !string.IsNullOrWhiteSpace(tier))
        {
            rows = rows.Where(r => string.Equals(r.TierName, tier, StringComparison.OrdinalIgnoreCase));
        }

        // Sort — a small allow-list of keys; default is the natural tier order.
        rows = (query.Sort, query.SortDescending) switch
        {
            ("name", false) => rows.OrderBy(r => r.EnglishName),
            ("name", true) => rows.OrderByDescending(r => r.EnglishName),
            ("tier", false) => rows.OrderBy(r => r.TierName),
            ("tier", true) => rows.OrderByDescending(r => r.TierName),
            ("createdAt", false) => rows.OrderBy(r => r.CreatedAt),
            ("createdAt", true) => rows.OrderByDescending(r => r.CreatedAt),
            _ => rows,
        };

        var materialised = rows.ToList();
        var total = materialised.Count;
        var top = query.Top <= 0 ? 20 : Math.Min(query.Top, 200);
        var page = materialised.Skip(query.Skip).Take(top).ToList();
        return GridPage<VipRosterRow>.Of(page, total, query);
    }

    private static bool Contains(string? value, string term) =>
        value is not null && value.Contains(term, StringComparison.OrdinalIgnoreCase);

    public async Task<VipRosterFile> ExportAsync(
        VipRosterExportFormat format,
        CancellationToken cancellationToken = default)
    {
        var rows = await GetRosterAsync(cancellationToken);
        return format == VipRosterExportFormat.Xlsx
            ? new VipRosterFile(
                BuildXlsx(rows),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "vip-welcome-roster.xlsx")
            : new VipRosterFile(
                BuildCsv(rows),
                "text/csv; charset=utf-8",
                "vip-welcome-roster.csv");
    }

    private static byte[] BuildCsv(IReadOnlyList<VipRosterRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", Headers.Select(CsvField)));
        foreach (var r in rows)
        {
            builder.AppendLine(string.Join(",", Cells(r).Select(CsvField)));
        }
        // UTF-8 BOM so Excel opens the Arabic columns in the right encoding.
        return Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(builder.ToString()))
            .ToArray();
    }

    private static byte[] BuildXlsx(IReadOnlyList<VipRosterRow> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("VIP welcome roster");
        for (var c = 0; c < Headers.Length; c++)
        {
            sheet.Cell(1, c + 1).Value = Headers[c];
            sheet.Cell(1, c + 1).Style.Font.Bold = true;
        }
        var row = 2;
        foreach (var r in rows)
        {
            var cells = Cells(r);
            for (var c = 0; c < cells.Length; c++)
            {
                sheet.Cell(row, c + 1).Value = cells[c];
            }
            row++;
        }
        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>The cell values in <see cref="Headers"/> order. Every free-text
    /// value is run through the codebase's OWASP CSV/XLSX-formula-injection guard
    /// (<see cref="ClosedXmlUserExcelService.SanitiseForExcel"/>, CWE-1236) so a
    /// VIP whose name / job-title / email starts with <c>= + - @</c> can't inject
    /// a formula into the file the technical teams open — the same guard the
    /// generic grid exporter applies.</summary>
    private static string[] Cells(VipRosterRow r) =>
    [
        ClosedXmlUserExcelService.SanitiseForExcel(r.MawjId),
        ClosedXmlUserExcelService.SanitiseForExcel(r.Honorific),
        ClosedXmlUserExcelService.SanitiseForExcel(r.TierName),
        ClosedXmlUserExcelService.SanitiseForExcel(r.EnglishName),
        ClosedXmlUserExcelService.SanitiseForExcel(r.ArabicName),
        ClosedXmlUserExcelService.SanitiseForExcel(r.DisplayName),
        ClosedXmlUserExcelService.SanitiseForExcel(r.JobTitle),
        ClosedXmlUserExcelService.SanitiseForExcel(r.JobTitleArabic),
        ClosedXmlUserExcelService.SanitiseForExcel(r.PreferredLanguage),
        ClosedXmlUserExcelService.SanitiseForExcel(r.Email),
        ClosedXmlUserExcelService.SanitiseForExcel(r.Mobile),
        ClosedXmlUserExcelService.SanitiseForExcel(r.ReferenceNumber),
        ClosedXmlUserExcelService.SanitiseForExcel(r.AccountState),
        r.HasVipPhoto ? "Yes" : "No",
        r.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
    ];

    /// <summary>RFC-4180 CSV field quoting — wrap in quotes and double any
    /// embedded quote when the value carries a comma, quote, or newline.</summary>
    private static string CsvField(string value)
    {
        if (string.IsNullOrEmpty(value)) { return string.Empty; }
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}
