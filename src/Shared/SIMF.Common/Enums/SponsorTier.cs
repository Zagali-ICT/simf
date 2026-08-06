namespace SIMF.Common.Enums;

/// <summary>
/// The sponsorship tier a <c>Sponsor</c> belongs to.
/// The public sponsors screen groups logos under a heading per tier, highest
/// tier first; <see cref="Platinum"/> renders before <see cref="Gold"/> before
/// <see cref="Silver"/> before <see cref="Bronze"/>. The integer values are the
/// public sort weight (lower = shown first) AND the persisted wire value, so
/// they follow the same additive-only discipline as the frozen enums in
/// <c>SIMF.Common/Enums</c>: never renumber or rename an existing case; a new
/// tier appends a new value that does not collide. The gaps (10/20/30/40) leave
/// room to insert an intermediate tier later without renumbering.
/// </summary>
public enum SponsorTier //add translations
{
    /// <summary>Top tier — rendered first, largest logos.</summary>
    Platinum = 10,

    /// <summary>Second tier.</summary>
    Gold = 20,

    /// <summary>Third tier.</summary>
    Silver = 30,

    /// <summary>Fourth tier.</summary>
    Bronze = 40,
}
