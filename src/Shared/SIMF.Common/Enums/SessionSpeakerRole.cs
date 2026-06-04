namespace SIMF.Common.Enums;

/// <summary>
/// B9 — D-225 (FDS-004 §5.4): the role a speaker plays in a specific session —
/// "speaker or host" per the spec. Modelled on the JOIN (a person can be the
/// host of one session and a speaker in another), not on the Speaker entity.
/// Int-backed, additive-only discipline (never renumber/rename an existing
/// case); a future role (e.g. Moderator) appends a new non-colliding value.
/// </summary>
public enum SessionSpeakerRole
{
    /// <summary>A presenting speaker (default).</summary>
    Speaker = 0,

    /// <summary>The session host / chair (FDS-004 §5.4 "host").</summary>
    Host = 1,
}
