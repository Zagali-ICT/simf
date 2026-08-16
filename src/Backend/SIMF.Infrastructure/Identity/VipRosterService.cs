// Tests: SIMF.Api.Tests/VipRosterTests.cs
using System.Linq.Expressions;
using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Grids;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Common.Grids;
using SIMF.Infrastructure.Excel;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// Builds the VVIP/VIP welcome roster (موج) and renders it as CSV
/// or Excel. The guests and their admission state come from
/// <see cref="SimfAppDbContext"/>; only the email needs
/// <see cref="SimfIdentityDbContext"/>, in one batched query over the
/// subset who hold an account at all.
/// </summary>
internal sealed class VipRosterService(
    SimfIdentityDbContext dbContext,
    SimfAppDbContext appDbContext)
    : IVipRosterService
{
    // The audience-side tiers the roster covers. Matches the seeded names.
    private static readonly string[] TierNames = { "VVIP", "VIP" };

    /// <summary>
    /// The grid contract for the on-screen roster at /admin/visitors/vip/export.
    ///
    /// <para>The email is NOT here, and cannot be: it lives on the Identity
    /// database, which this list is forbidden to join. It is resolved per page
    /// below, so it still shows in the column — it just cannot be sorted, filtered
    /// or searched on. Neither is the mobile: those columns carry a randomized
    /// AES-GCM value converter, so a predicate over them would translate, run, and
    /// match nothing at all.</para>
    /// </summary>
    private static readonly GridColumns<UserProfile> Columns = new GridColumns<UserProfile>()
        .Add("tier", profile => profile.ProfileType!.Name)
        .Add("name", profile => profile.Name, searchable: true)
        .Add("nameArabic", profile => profile.NameArabic, searchable: true)
        .Add("honorific", profile => profile.Honorific, searchable: true)
        .Add("mawjId", profile => profile.MawjId, searchable: true)
        .Add("reference", profile => profile.ReferenceNumber, searchable: true)
        .Add("createdAt", profile => profile.CreatedAt)
        // VVIP before VIP, then alphabetical — the roster's own order, and the one
        // the desk reads it in. Descending on the tier NAME gives it because the
        // scope below admits only those two values and "VIP" sorts before "VVIP".
        .DefaultOrder("tier", descending: true)
        .DefaultOrder("name")
        .PageSize(fallback: 20, max: 200);

    /// <summary>The App-side half of a roster row. Named rather than anonymous
    /// because both the whole-roster read and the paged read project it and share
    /// one mapper, so the shape has to have a name they can both say.</summary>
    private sealed record RosterProfile(
        Guid ProfileId,
        Guid? UserId,
        string Name,
        string NameArabic,
        string? Honorific,
        string? JobTitle,
        string? JobTitleArabic,
        string? MawjId,
        string? PreferredLanguage,
        string TierName,
        string TierNameArabic,
        string? SaudiMobile,
        string? InternationalMobile,
        string? ReferenceNumber,
        AccountState AdmissionState,
        bool HasVipPhoto,
        DateTime CreatedAt);

    /// <summary>The projection both reads use. The two mobile columns are pulled
    /// separately and chosen between in memory: they are value-converted, so the
    /// choice has to be made on the decrypted values, after materialisation.</summary>
    private static readonly Expression<Func<UserProfile, RosterProfile>> ToProfile =
        profile => new RosterProfile(
            profile.Id,
            profile.UserId,
            profile.Name,
            profile.NameArabic,
            profile.Honorific,
            profile.JobTitle,
            profile.JobTitleArabic,
            profile.MawjId,
            profile.PreferredLanguage,
            profile.ProfileType!.Name,
            profile.ProfileType.NameArabic,
            profile.SaudiMobile,
            profile.InternationalMobile,
            profile.ReferenceNumber,
            profile.AdmissionState,
            // The welcome photo is stored against the ACCOUNT and fetched by
            // account id, so a guest without one has no route to it however
            // the column reads. Answering true here would render a link the
            // page has no id to build.
            profile.VipPhotoFileId != null && profile.UserId != null,
            profile.CreatedAt);

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
        var profiles = await Roster()
            .Select(ToProfile)
            .ToListAsync(cancellationToken);

        if (profiles.Count == 0)
        {
            return Array.Empty<VipRosterRow>();
        }

        var emails = await EmailsAsync(profiles, cancellationToken);

        return profiles
            // VVIP before VIP, then alphabetical by English name.
            .OrderBy(p => p.TierName == "VVIP" ? 0 : 1)
            .ThenBy(p => p.Name)
            .Select(p => ToRow(p, emails))
            .ToList();
    }

    public async Task<GridPage<VipRosterRow>> GetRosterPageAsync(
        GridQuery query,
        CancellationToken cancellationToken = default)
    {
        // The whole roster used to be loaded and then filtered, sorted and paged in
        // memory, so every page read every VVIP row. The filter, the order and the
        // page window are now SQL; only the email lookup is left to do per page, and
        // it is the one thing that cannot be SQL here.
        var page = await Roster()
            .ToGridPageAsync(query, Columns, profile => profile.Id, ToProfile, cancellationToken);

        var emails = await EmailsAsync(page.Items, cancellationToken);

        var rows = page.Items.Select(p => ToRow(p, emails)).ToList();
        return GridPage<VipRosterRow>.Of(rows, page.Total, page.Skip, page.Top);
    }

    /// <summary>The roster's scope: the audience-side VVIP / VIP profiles. A grid
    /// filter composes after it and so can never widen it.</summary>
    private IQueryable<UserProfile> Roster() =>
        appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.ProfileType != null
                && p.ProfileType.IsForVisitor
                && TierNames.Contains(p.ProfileType.Name));

    /// <summary>The one Identity-side field the roster shows, for the guests who
    /// hold an account at all — the roster no longer excludes the ones who do not.
    /// Read on its own query against the other database; the two are never
    /// joined.</summary>
    private async Task<Dictionary<Guid, string?>> EmailsAsync(
        IReadOnlyList<RosterProfile> profiles, CancellationToken cancellationToken)
    {
        var ids = profiles.Where(p => p.UserId != null).Select(p => p.UserId!.Value).ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string?>();
        }

        return await dbContext.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email, cancellationToken);
    }

    private static VipRosterRow ToRow(RosterProfile p, Dictionary<Guid, string?> emails)
    {
        string? email = null;
        if (p.UserId is { } accountId)
        {
            emails.TryGetValue(accountId, out email);
        }

        return new VipRosterRow(
            p.ProfileId,
            p.UserId,
            // The profile name, which is what the desk registered the
            // guest as and all there is for one who never signed up.
            // Same order the gate resolver uses: the attendee record
            // wins, because DisplayName is the greeting field only.
            p.Name,
            p.Name,
            p.NameArabic,
            p.Honorific,
            p.JobTitle,
            p.JobTitleArabic,
            p.MawjId,
            p.PreferredLanguage,
            p.TierName,
            p.TierNameArabic,
            email ?? string.Empty,
            string.IsNullOrWhiteSpace(p.SaudiMobile) ? p.InternationalMobile : p.SaudiMobile,
            p.ReferenceNumber,
            // Admission is the profile's own state. Reading it off the
            // account used to render an approved accountless guest as
            // "PendingApproval", telling the PR desk a VVIP who was
            // cleared to attend had not been.
            p.AdmissionState.ToString(),
            p.HasVipPhoto,
            p.CreatedAt);
    }

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
