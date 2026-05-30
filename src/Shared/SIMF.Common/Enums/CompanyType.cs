namespace SIMF.Common.Enums;

/// <summary>
/// D-199 #3 (additive new enum) — the kind of <c>Company</c> the Control
/// Panel provisions. Exhibitor / sponsor companies are created CP-side only;
/// the integer values are the persisted wire value, so they follow the same
/// additive-only discipline as the frozen enums in this namespace: never
/// renumber or rename an existing case; a new kind appends a new value that
/// does not collide.
/// </summary>
public enum CompanyType
{
    /// <summary>An exhibition company that staffs a booth.</summary>
    Exhibitor = 0,

    /// <summary>An event sponsor company.</summary>
    Sponsor = 1,
}
