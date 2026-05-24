namespace SIMF.Domain.IdentityAccess;

/// <summary>
/// The hardcoded SIMF user type (P7 — decision D-048). One column on
/// <see cref="SimfUser"/>; determines where the user can sign in and
/// whether RBAC applies:
///
/// <list type="bullet">
///   <item><see cref="Visitor"/> — public attendee. Signs in on the App
///     (and the public Website). Never carries an RBAC role; subtype
///     (VVIP / VIP / Gold / …) lives in <see cref="ProfileType"/>.</item>
///   <item><see cref="Other"/> — event team / partner (Staff, Exhibitor,
///     Sponsor, Media, …). Signs in on the App. Never carries an RBAC
///     role; subtype lives in <see cref="ProfileType"/>.</item>
///   <item><see cref="Admin"/> — Control Panel administrator. Signs in
///     on the CP only. **The only UserType that can perform management
///     operations**, every action gated by an RBAC role
///     (Administrator today; future fine-grained roles plug in here).</item>
/// </list>
///
/// <para>The numeric values are stable — they are persisted as integers
/// in the database. <see cref="Visitor"/> is <c>0</c> so the
/// least-privileged surface is the default for any row that loses its
/// type metadata.</para>
/// </summary>
public enum UserType
{
    /// <summary>Default — the least-privileged surface (App / Website).</summary>
    Visitor = 0,

    /// <summary>Event team / partner — App access, no RBAC.</summary>
    Other = 1,

    /// <summary>Control Panel administrator — RBAC-gated.</summary>
    Admin = 2,
}
