namespace SIMF.Common.Enums;

/// <summary>
/// B3 / D-221 (additive new enum) — a user's gender (الجنس), captured on the
/// profile / sign-up screen (Mockup screen 05). The integer values are the
/// persisted wire value, so they follow the same additive-only discipline as
/// the frozen enums in this namespace: never renumber or rename an existing
/// case; a new value appends and must not collide. <see cref="Unspecified"/>
/// is the 0-slot so existing profile rows backfill cleanly and a user who has
/// not picked is representable.
/// </summary>
public enum Gender
{
    /// <summary>Not stated yet (default for rows created before D-221, and for
    /// users who submit the form without choosing).</summary>
    Unspecified = 0,

    /// <summary>Male — ذكر.</summary>
    Male = 1,

    /// <summary>Female — أنثى.</summary>
    Female = 2,
}
