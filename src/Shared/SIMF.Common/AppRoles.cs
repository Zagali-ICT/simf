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

    /// <summary>D-148 — operator role for the Gate Module. Holders carry
    /// the <see cref="Permissions.GatesOperate"/> and
    /// <see cref="Permissions.GatesViewOwnReports"/> permissions. Operators
    /// authenticate against the CP surface (they use the operator console).</summary>
    public const string GateOperator = "GateOperator";

    /// <summary>Every CP-side RBAC role. Today: Administrator + GateOperator.</summary>
    public static readonly IReadOnlyList<string> CpRoles =
        [Administrator, GateOperator];
}

/// <summary>D-148 — Gate Module permission names. Per the
/// SIMF-RPM-001 model, an Administrator holds every permission; a
/// <see cref="AppRoles.GateOperator"/> holds only the gate permissions.</summary>
public static class Permissions
{
    public const string GatesManage = "Gates.Manage";
    public const string GatesOperate = "Gates.Operate";
    public const string GatesViewOwnReports = "Gates.ViewOwnReports";
}
