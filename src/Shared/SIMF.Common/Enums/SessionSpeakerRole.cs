namespace SIMF.Common.Enums;

/// <summary>
/// The role a speaker plays in a specific session — either
/// speaker or host. Modelled on the JOIN (a person can be the
/// host of one session and a speaker in another), not on the Speaker entity.
/// Int-backed, additive-only discipline (never renumber/rename an existing
/// case); a future role (e.g. Moderator) appends a new non-colliding value.
/// </summary>
public enum SessionSpeakerRole
{
    /// <summary>A presenting speaker (default).</summary>
    Speaker = 0,

    /// <summary>The session host / chair.</summary>
    Host = 1,
}
