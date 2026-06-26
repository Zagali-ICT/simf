namespace SIMF.Common.Enums;

/// <summary>
/// Wave 3 (Figma 1439:11881 "العارض") — the exhibitor's tier, shown as the pill
/// under the company name on the exhibitor-detail screen (e.g. "عارض بريميوم").
/// Optional on <c>Exhibitor</c> — a null tier renders no pill. Like
/// <see cref="SponsorTier"/>, the integer values are the persisted wire value, so
/// they follow the same additive-only discipline: never renumber or rename an
/// existing case; a new tier appends a new value that does not collide. The gaps
/// (10/20/30/40) leave room to insert an intermediate tier without renumbering.
/// </summary>
public enum ExhibitorTier
{
    /// <summary>Top tier — "بريميوم" (Premium).</summary>
    Premium = 10,

    /// <summary>Second tier.</summary>
    Gold = 20,

    /// <summary>Third tier.</summary>
    Silver = 30,

    /// <summary>Fourth tier.</summary>
    Bronze = 40,
}
