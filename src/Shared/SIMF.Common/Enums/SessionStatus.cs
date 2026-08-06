namespace SIMF.Common.Enums;

/// <summary>
/// The broadcast lifecycle of a programme <c>Session</c>. The Scientific Committee
/// drives the transitions in the Control Panel:
/// <c>Scheduled → Held → Recorded → Published</c> (and the reverse
/// adjacent steps). <c>Published</c> stamps <c>PublishedAt</c> and is the
/// state in which the app shows the recording + the recorded Q&amp;A.
///
/// <para><b>Distinct from <c>IsActive</c>:</b> <c>IsActive</c> is the
/// soft-delete flag (hide the row); this is the broadcast lifecycle. A
/// session can be Scheduled-and-active, or Published-and-active; a
/// soft-deleted session keeps whatever status it had.</para>
///
/// <para>Int-backed, additive-only discipline (never renumber/rename an
/// existing case). A future true-live state, deferred pending a
/// live-video provider, would append a new non-colliding value.</para>
/// </summary>
public enum SessionStatus
{
    /// <summary>The default — a future or upcoming session (the agenda
    /// state). Every newly created session starts here.</summary>
    Scheduled = 0,

    /// <summary>The session has taken place; no recording yet.</summary>
    Held = 1,

    /// <summary>A recording has been produced for the session (the
    /// Committee links/uploads it). Not yet visible to the public.</summary>
    Recorded = 2,

    /// <summary>The Committee has published the session — the recording +
    /// recorded Q&amp;A are visible to the app. <c>PublishedAt</c> is set.</summary>
    Published = 3,
}
