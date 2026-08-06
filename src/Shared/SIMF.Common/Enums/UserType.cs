using System.ComponentModel.DataAnnotations;
using SIMF.Common.Resources.Enums;

namespace SIMF.Common.Enums;

/// <summary>
/// The hardcoded SIMF user type. D-186 collapsed the previous three-way
/// (Visitor / Other / Admin) into a two-way (Visitor / Admin):
///
/// <list type="bullet">
///   <item><see cref="Visitor"/> — every non-administrator account.
///     Public attendees, partners, sponsors, exhibitors, media — anyone
///     who signs in via the App or the public Website. Never carries an
///     RBAC role. Subtype (audience vs partner) is now expressed via
///     <c>ProfileType.IsVisitor</c> + <c>ProfileType.MobileAppRole</c>;
///     the audience / partner split that used to live on a separate
///     UserType value moves entirely onto the ProfileType row.</item>
///   <item><see cref="Admin"/> — Control Panel administrator. Signs in
///     on the CP only. The only UserType that can perform management
///     operations, every action gated by an RBAC role (Administrator
///     today; future fine-grained roles plug in here).</item>
/// </list>
///
/// <para><b>D-186 freeze-break note.</b> The previous enum had
/// <c>Other = 1</c> between <see cref="Visitor"/> and <see cref="Admin"/>.
/// The integer 1 is now reserved — Admin stays at 2 so every existing
/// Admin row in the DB keeps its persisted column value. A data
/// migration converts every pre-D-186 <c>UserType = 1</c> (Other) row
/// to <c>UserType = 0</c> (Visitor) and stamps <c>IsVisitor = false</c>
/// on the linked <c>ProfileType</c> row.</para>
///
/// <para>The numeric values are stable — they are persisted as integers
/// in the database. <see cref="Visitor"/> is <c>0</c> so the
/// least-privileged surface is the default for any row that loses its
/// type metadata.</para>
/// </summary>
public enum UserType
{
    /// <summary>Default — the least-privileged surface (App / Website).
    /// Every non-admin SIMF account is a Visitor; the audience-vs-partner
    /// distinction lives on <c>ProfileType.IsVisitor</c>.</summary>
    [Display(Description = nameof(ResUserType.Visitor), ResourceType = typeof(ResUserType))]
    Visitor = 0,

    // 1 is reserved — used to be `Other`.

    /// <summary>Control Panel administrator — RBAC-gated.</summary>
    [Display(Description = nameof(ResUserType.Admin), ResourceType = typeof(ResUserType))]
    Admin = 2,
}
