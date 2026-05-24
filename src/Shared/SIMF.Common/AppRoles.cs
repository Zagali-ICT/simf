namespace SIMF.Common;

/// <summary>
/// The Control Panel **RBAC role** names. Per the P7 model (decision
/// D-048), RBAC roles apply **only** to users with
/// <c>UserType = Admin</c>; other users (<c>Visitor</c>, <c>Other</c>)
/// never carry an RBAC role — their kind comes from the
/// <c>UserType</c> column and their subtype from <c>ProfileType</c>.
///
/// <para>Today only one role is needed — <see cref="Administrator"/>.
/// Future fine-grained Admin-side roles (e.g. <c>AuditViewer</c>,
/// <c>RegistrationApprover</c>) plug in here.</para>
///
/// <para>The P4-era "reviewer roles" (Staff / Scientific / Security)
/// were removed by P7 — they are now <c>ProfileType</c> rows with
/// <c>UserType = Other</c>, not RBAC roles.</para>
/// </summary>
public static class AppRoles
{
    public const string Administrator = "Administrator";

    /// <summary>Every CP-side RBAC role. Today: just Administrator.</summary>
    public static readonly IReadOnlyList<string> CpRoles =
        [Administrator];
}
