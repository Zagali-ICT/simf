// Report grids receive enums as raw strings — the API sends someEnum.ToString().
// Rendered verbatim that showed an operator "HolderNotApproved", "CheckIn",
// "PerSession" and "PendingApproval", and on the ARABIC page every enum column
// stayed in English, which matters more: Arabic is the primary language.
//
// ReportPageBase.EnumLabel resolves Admin.Reports.Enum.<Group>.<Member>. It
// falls back to the raw member for an unknown one, so WITHOUT the first test
// below a newly added enum value would ship quietly showing an identifier
// again. That test is the actual guard; the render tests only prove the cells
// are wired to it.
using System.Xml.Linq;
using Bunit;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.ControlPanel.Components.Pages.Admin.Reports;
using SIMF.Contracts.Reporting;

namespace SIMF.ControlPanel.Tests;

public sealed class ReportEnumLabelTests : CpComponentTestBase
{
    private const string Prefix = "Admin.Reports.Enum.";

    /// <summary>
    /// Every enum a report grid renders, paired with the members that must have
    /// a label. <c>PartnerTier</c> is deliberately not an enum type: the
    /// partners report flattens <see cref="ExhibitorTier"/> (Premium…) and
    /// <see cref="SponsorTier"/> (Platinum…) into one Tier column, so by the
    /// time the CP sees it the originating type is gone and both resolve under
    /// one group.
    /// </summary>
    public static TheoryData<string, string[]> RenderedEnums() => new()
    {
        { "ScanDirection", Enum.GetNames<ScanDirection>() },
        { "ScanOutcome", Enum.GetNames<ScanOutcome>() },
        { "DenialReasonCode", Enum.GetNames<DenialReasonCode>() },
        { "QuestionPhase", Enum.GetNames<QuestionPhase>() },
        { "QuestionStatus", Enum.GetNames<QuestionStatus>() },
        { "SessionQuestionRecipient", Enum.GetNames<SessionQuestionRecipient>() },
        { "RatingScope", Enum.GetNames<RatingScope>() },
        { "AccountState", Enum.GetNames<AccountState>() },
        { "MeetingRequestStatus", Enum.GetNames<MeetingRequestStatus>() },
        {
            "PartnerTier",
            Enum.GetNames<ExhibitorTier>().Union(Enum.GetNames<SponsorTier>()).ToArray()
        },
    };

    /// <summary>
    /// The Control Panel's Resources folder, found by walking up from the test
    /// assembly until the project appears.
    ///
    /// <para>Deliberately NOT "the directory containing .git". In a git
    /// WORKTREE `.git` is a FILE, so that walk skips the worktree root and
    /// lands on the main checkout — the test then audits a different tree than
    /// the one being compiled. That cost a red run before it was changed.</para>
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
            .Where(n => n.StartsWith(Prefix, StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

    [Theory]
    [MemberData(nameof(RenderedEnums))]
    public void Every_rendered_enum_member_has_a_label_in_both_languages(
        string group, string[] members)
    {
        var english = KeysIn("Strings.resx");
        var arabic = KeysIn("Strings.ar.resx");

        var missing = members
            .SelectMany(m => new[]
            {
                english.Contains($"{Prefix}{group}.{m}") ? null : $"Strings.resx: {Prefix}{group}.{m}",
                arabic.Contains($"{Prefix}{group}.{m}") ? null : $"Strings.ar.resx: {Prefix}{group}.{m}",
            })
            .Where(x => x is not null)
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"A {group} member has no label, so a report grid will show an operator "
            + "the raw enum member — and the Arabic page will show English. Add it "
            + "to BOTH resx files.\n"
            + string.Join('\n', missing!));
    }

    [Fact]
    public void No_orphan_labels_survive_for_values_that_no_longer_exist()
    {
        // A key for a deleted member is dead weight that reads as coverage, and
        // would hide the removal from the test above.
        var expected = RenderedEnums()
            .Select(row => (Group: (string)row[0], Members: (string[])row[1]))
            .SelectMany(x => x.Members.Select(m => $"{Prefix}{x.Group}.{m}"))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var file in new[] { "Strings.resx", "Strings.ar.resx" })
        {
            var orphans = KeysIn(file).Where(k => !expected.Contains(k)).OrderBy(k => k).ToList();
            Assert.True(
                orphans.Count == 0,
                $"{file} has enum labels for members that are not rendered by any "
                + "report:\n" + string.Join('\n', orphans));
        }
    }

    /// <summary>
    /// Each enum-backed grid cell, and the group it must resolve through.
    /// </summary>
    public static TheoryData<string, string> WiredCells() => new()
    {
        { "GateActivityReport.razor", "ScanDirection" },
        { "GateActivityReport.razor", "ScanOutcome" },
        { "GateActivityReport.razor", "DenialReasonCode" },
        { "EngagementReport.razor", "SessionQuestionRecipient" },
        { "EngagementReport.razor", "QuestionStatus" },
        { "EngagementReport.razor", "QuestionPhase" },
        { "RatingsReport.razor", "RatingScope" },
        { "RegistrationsReport.razor", "AccountState" },
        { "MeetingsReport.razor", "MeetingRequestStatus" },
        { "PartnersReport.razor", "PartnerTier" },
    };

    [Theory]
    [MemberData(nameof(WiredCells))]
    public void Every_enum_cell_is_wired_through_the_resolver(string page, string group)
    {
        // Added because the completeness test above CANNOT see this. Reverting a
        // cell to @context.Scope leaves every label in place, so that test stays
        // green while the grid quietly prints "PerSession" again — proven by
        // mutation: un-wiring the ratings cell passed until this test existed.
        var dir = Path.Combine(
            Directory.GetParent(ResourcesDirectory())!.FullName,
            "Components", "Pages", "Admin", "Reports");
        var source = File.ReadAllText(Path.Combine(dir, page));

        Assert.True(
            source.Contains($"EnumLabel(\"{group}\"", StringComparison.Ordinal),
            $"{page} no longer resolves its {group} cell through EnumLabel, so that "
            + "column will render the raw enum member and stay English in Arabic.");
    }

    private void StubGates(string? denialReason, string direction, string outcome) =>
        JSInterop.Setup<ApiResult<ReportPage<GateActivityReportRow>>>(
                "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<ReportPage<GateActivityReportRow>>.Ok(
                new ReportPage<GateActivityReportRow>(
                    [new GateActivityReportRow(
                        ScanId: 1,
                        GateName: "Main Venue Gate",
                        ScannedDisplay: "23-11-2026 09:30 AM",
                        Direction: direction,
                        Outcome: outcome,
                        DenialReason: denialReason,
                        VisitorName: "Hind Al-Zahrani",
                        ProfileTypeName: "Visitor")],
                    Total: 1, Skip: 0, Top: 25, Totals: [])));

    [Fact]
    public void The_gate_grid_resolves_direction_outcome_and_denial_reason()
    {
        // The test localizer echoes the key, so seeing the KEY proves the cell
        // went through the localizer rather than printing the payload verbatim.
        StubGates(
            nameof(DenialReasonCode.HolderNotApproved),
            nameof(ScanDirection.CheckIn),
            nameof(ScanOutcome.Denied));

        var markup = RenderComponent<GateActivityReport>().Markup;

        Assert.Contains($"{Prefix}ScanDirection.CheckIn", markup);
        Assert.Contains($"{Prefix}ScanOutcome.Denied", markup);
        Assert.Contains($"{Prefix}DenialReasonCode.HolderNotApproved", markup);
    }

    [Fact]
    public void A_denied_scan_still_gets_the_off_pill_after_localisation()
    {
        // The pill variant is chosen from the RAW code. If that comparison were
        // ever moved onto the localised label, every denied scan would render
        // with the "allowed" colour the moment the culture changed — a wrong
        // signal on a security report, and invisible in English.
        StubGates(
            nameof(DenialReasonCode.HolderNotApproved),
            nameof(ScanDirection.CheckIn),
            nameof(ScanOutcome.Denied));

        var cut = RenderComponent<GateActivityReport>();

        Assert.NotEmpty(cut.FindAll(".simf-pill--off"));
        Assert.Empty(cut.FindAll(".simf-pill--on"));
    }

    [Fact]
    public void An_allowed_scan_leaves_the_denial_reason_cell_empty()
    {
        // Null must stay blank, not become a stray label or the word "null".
        StubGates(null, nameof(ScanDirection.CheckOut), nameof(ScanOutcome.Allowed));

        var markup = RenderComponent<GateActivityReport>().Markup;

        Assert.DoesNotContain($"{Prefix}DenialReasonCode.", markup);
        Assert.DoesNotContain("null", markup, StringComparison.OrdinalIgnoreCase);
    }
}
