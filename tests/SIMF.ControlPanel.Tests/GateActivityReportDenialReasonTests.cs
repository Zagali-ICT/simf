// The gate activity report used to print the raw enum member in its "Denial
// reason" column: an operator reviewing refused entries read "HolderNotApproved"
// rather than "Account not approved". Found by driving E2E-RPT-004 against
// seeded scans.
//
// The API is unchanged and stays that way. A stable code is the right thing on
// the wire and in the XLSX export, which is a data file people pivot on. Only
// the on-screen cell becomes words, in the reader's language.
using System.Xml.Linq;
using Bunit;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.ControlPanel.Components.Pages.Admin.Reports;
using SIMF.Contracts.Reporting;

namespace SIMF.ControlPanel.Tests;

public sealed class GateActivityReportDenialReasonTests : CpComponentTestBase
{
    private const string KeyPrefix = "Admin.Reports.DenialReason.";

    /// <summary>
    /// The Control Panel's Resources folder, found by walking up from the test
    /// assembly until the project itself appears.
    ///
    /// <para>Deliberately NOT "walk up to the directory containing .git". In a
    /// git WORKTREE, <c>.git</c> is a FILE, not a directory, so that walk skips
    /// straight past the worktree root and lands on the main checkout — and the
    /// test then audits a different tree's resx than the one being built. This
    /// cost a red run before the anchor was changed.</para>
    /// </summary>
    private static string ResourcesDirectory()
    {
        var relative = Path.Combine("src", "ControlPanel", "SIMF.ControlPanel", "Resources");
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, relative)))
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null, $"Could not find '{relative}' above {AppContext.BaseDirectory}");
        return Path.Combine(dir!.FullName, relative);
    }

    private static HashSet<string> KeysIn(string file) =>
        XDocument.Load(Path.Combine(ResourcesDirectory(), file))
            .Root!.Elements("data")
            .Select(d => d.Attribute("name")!.Value)
            .Where(n => n.StartsWith(KeyPrefix, StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

    private static ReportPage<GateActivityReportRow> PageWith(string? denialReason) =>
        new(
            [new GateActivityReportRow(
                ScanId: 1,
                GateName: "Main Venue Gate",
                ScannedDisplay: "23-11-2026 09:30 AM",
                Direction: nameof(ScanDirection.CheckIn),
                Outcome: nameof(ScanOutcome.Denied),
                DenialReason: denialReason,
                VisitorName: "Hind Al-Zahrani",
                ProfileTypeName: "Visitor")],
            Total: 1, Skip: 0, Top: 25, Totals: []);

    private void Stub(ReportPage<GateActivityReportRow> page) =>
        JSInterop.Setup<ApiResult<ReportPage<GateActivityReportRow>>>(
                "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<ReportPage<GateActivityReportRow>>.Ok(page));

    [Fact]
    public void Every_denial_reason_has_a_label_in_both_languages()
    {
        // THE test. The page falls back to the raw code for an unknown one, so a
        // new DenialReasonCode member would ship silently showing "SomeNewCase"
        // to an operator. This makes that a build failure instead.
        var english = KeysIn("Strings.resx");
        var arabic = KeysIn("Strings.ar.resx");

        var missing = Enum.GetNames<DenialReasonCode>()
            .SelectMany(name => new[]
            {
                english.Contains(KeyPrefix + name) ? null : $"Strings.resx: {KeyPrefix}{name}",
                arabic.Contains(KeyPrefix + name) ? null : $"Strings.ar.resx: {KeyPrefix}{name}",
            })
            .Where(m => m is not null)
            .ToList();

        Assert.True(
            missing.Count == 0,
            "A DenialReasonCode has no label, so the gate report will show an "
            + "operator the raw enum member. Add it to BOTH resx files.\n"
            + string.Join('\n', missing!));

        // No orphans either: a key for a value that no longer exists is dead
        // weight that reads as coverage.
        var known = Enum.GetNames<DenialReasonCode>()
            .Select(n => KeyPrefix + n)
            .ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain(english, k => !known.Contains(k));
        Assert.DoesNotContain(arabic, k => !known.Contains(k));
    }

    [Fact]
    public void A_known_reason_renders_its_label_not_the_enum_member()
    {
        // The test localizer echoes the key, so seeing the KEY proves the cell
        // went through the localizer rather than printing the payload verbatim.
        Stub(PageWith(nameof(DenialReasonCode.HolderNotApproved)));

        var cut = RenderComponent<GateActivityReport>();

        Assert.Contains(KeyPrefix + nameof(DenialReasonCode.HolderNotApproved), cut.Markup);
    }

    [Fact]
    public void An_allowed_scan_leaves_the_cell_empty()
    {
        // Null must stay blank, not become a stray label or the word "null".
        Stub(PageWith(null));

        var cut = RenderComponent<GateActivityReport>();

        Assert.DoesNotContain(KeyPrefix, cut.Markup);
        Assert.DoesNotContain("null", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }
}
