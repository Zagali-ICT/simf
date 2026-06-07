namespace SIMF.Common;

/// <summary>
/// CMS content-block keys for the public marketing landing's editorial
/// sections below the hero — About (Mockup §01), the global-landscape stats
/// strip (§02), the Pillars header (§03) and the Goals block (§08). Shared so
/// the Website's <c>/content/site</c> proxy (which requests + maps them) and
/// the seeder (which seeds the default bilingual copy) cannot drift on the key
/// strings — a mismatch would silently drop the section to its built-in
/// markup. Keys are lowercase to match the CMS service's key normalisation,
/// and dotted so the proxy can project them into the landing's nested content
/// model (e.g. <c>goals.item1.t</c> → <c>goals.item1.t</c>).
///
/// <para>The five Pillar rows and the insights marquee are list/animation data
/// that stay in the page's built-in arrays; only the editorial text above is
/// CMS-driven here.</para>
/// </summary>
public static class LandingSectionContentKeys
{
    // About — Mockup §01.
    public const string AboutEyebrow = "about.eyebrow";
    public const string AboutHeading = "about.h2";
    public const string AboutBody1 = "about.p1";
    public const string AboutBody2 = "about.p2";

    // Global-landscape stats strip — Mockup §02: intro copy + the four
    // headline figures and their labels.
    public const string StatsEyebrow = "stats.eyebrow";
    public const string StatsIntro = "stats.intro";
    public const string StatsHeading = "stats.h3";
    public const string StatsCount1 = "stats.count1";
    public const string StatsLabel1 = "stats.label1";
    public const string StatsCount2 = "stats.count2";
    public const string StatsLabel2 = "stats.label2";
    public const string StatsCount3 = "stats.count3";
    public const string StatsLabel3 = "stats.label3";
    public const string StatsCount4 = "stats.count4";
    public const string StatsLabel4 = "stats.label4";

    // Pillars header — Mockup §03 (the five rows stay in the page array).
    public const string PillarsEyebrow = "pillars.eyebrow";
    public const string PillarsHeading = "pillars.h2";
    public const string PillarsBody = "pillars.p";

    // Goals — Mockup §08: header + the five goal cards.
    public const string GoalsEyebrow = "goals.eyebrow";
    public const string GoalsHeading = "goals.h2";
    public const string GoalsBody = "goals.p";
    public const string GoalsButton = "goals.btn";
    public const string Goal1Title = "goals.item1.t";
    public const string Goal1Body = "goals.item1.d";
    public const string Goal2Title = "goals.item2.t";
    public const string Goal2Body = "goals.item2.d";
    public const string Goal3Title = "goals.item3.t";
    public const string Goal3Body = "goals.item3.d";
    public const string Goal4Title = "goals.item4.t";
    public const string Goal4Body = "goals.item4.d";
    public const string Goal5Title = "goals.item5.t";
    public const string Goal5Body = "goals.item5.d";

    /// <summary>All landing-section keys, in landing display order. The
    /// Website proxy requests this set in the same CMS batch as the hero
    /// keys.</summary>
    public static readonly string[] All =
    [
        AboutEyebrow, AboutHeading, AboutBody1, AboutBody2,
        StatsEyebrow, StatsIntro, StatsHeading,
        StatsCount1, StatsLabel1, StatsCount2, StatsLabel2,
        StatsCount3, StatsLabel3, StatsCount4, StatsLabel4,
        PillarsEyebrow, PillarsHeading, PillarsBody,
        GoalsEyebrow, GoalsHeading, GoalsBody, GoalsButton,
        Goal1Title, Goal1Body, Goal2Title, Goal2Body, Goal3Title, Goal3Body,
        Goal4Title, Goal4Body, Goal5Title, Goal5Body,
    ];
}
