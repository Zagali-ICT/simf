namespace SIMF.Common.Enums;

/// <summary>
/// D-161 — the resolved mobile-app authority a signed-in user carries
/// into the Flutter app (SIMF-FDS-002 §8.5; SIMF-MAA-001).
///
/// <para>The mapping is data, not code:</para>
/// <list type="bullet">
///   <item><b>Visitor</b> (1) — any <see cref="UserType.Visitor"/>. Resolved
///     directly from <see cref="UserType"/>, not from <c>ProfileType</c>.</item>
///   <item><b>Staff</b> (2) and <b>Moderator</b> (3) — assigned per
///     <c>ProfileType</c> via the admin-curated <c>ProfileType.MobileAppRole</c>
///     column. Default seed maps Staff / Volunteer → <c>Staff</c> and
///     Programme Coordinator / Operations Lead → <c>Moderator</c>; admins
///     rebalance the mapping at runtime without a code change.</item>
///   <item><b>None</b> (0) — explicit "no in-app authority" for
///     partner-side profile types (D-186: ProfileType.IsVisitor=false
///     under the unified Visitor UserType) whose mobile-app effect is
///     "treated as a Visitor" (Exhibitor, Sponsor, Speaker, Press,
///     VIP Guest, Government, …).</item>
/// </list>
///
/// <para>The Flutter app's own privilege enum prefixes <b>Guest</b> = 0
/// for the unauthenticated case (absence of a JWT); the values here are
/// what the JWT claim <c>mobile_app_role</c> can carry post-sign-in.</para>
///
/// <para>Numeric values are stable — they are persisted on
/// <c>ProfileType.MobileAppRole</c> and travel on the wire as the
/// stringly-typed <c>mobile_app_role</c> claim.</para>
/// </summary>
public enum MobileAppRole
{
    /// <summary>Default — the profile type confers no in-app authority;
    /// the holder is treated as a Visitor by the Flutter app even though
    /// their <see cref="UserType"/> is <c>Other</c>.</summary>
    None = 0,

    /// <summary>The holder is a <see cref="UserType.Visitor"/> — assigned
    /// at JWT issuance time by reading the user-type column, not the
    /// profile-type. Stored here for completeness of the enum surface;
    /// no <c>ProfileType</c> row should carry this value.</summary>
    Visitor = 1,

    /// <summary>Limited operational authority — can perform staff
    /// functions in the Flutter app (check-in attendees at gates,
    /// look up information, print badges).</summary>
    Staff = 2,

    /// <summary>Elevated authority over content / users — can perform
    /// Staff functions plus moderate user-generated content and
    /// override certain holds (e.g. force-approve a flagged scan).</summary>
    Moderator = 3,
}
