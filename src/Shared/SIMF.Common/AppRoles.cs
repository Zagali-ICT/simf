namespace SIMF.Common;

/// <summary>
/// The Control Panel **RBAC role** names. Per the P7 model (decision
/// D-048), RBAC roles apply **only** to users with
/// <c>UserType = Admin</c>; other users (<c>Visitor</c>, <c>Other</c>)
/// never carry an RBAC role — their kind comes from the
/// <c>UserType</c> column and their subtype from <c>ProfileType</c>.
///
/// <para>Today the active CP roles are <see cref="Administrator"/>,
/// <see cref="GateOperator"/>, and <see cref="PublicRelations"/>. Future
/// fine-grained Admin-side roles (e.g. <c>AuditViewer</c>,
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

    /// <summary>D-168 (gap doc G5, PDF §2.7.3) — public-relations team
    /// role. Holders manage <c>Invitation</c> rows, view the VIP list, and
    /// dispatch guest-targeted notifications. Per the resolved open item
    /// G-OI-4 (auto-mode default), PublicRelations shares the existing
    /// CP "System" layout group rather than gaining its own layout — this
    /// can be split later if the team grows past one shared page set.</summary>
    public const string PublicRelations = "PublicRelations";

    /// <summary>Every CP-side RBAC role.</summary>
    public static readonly IReadOnlyList<string> CpRoles =
        [Administrator, GateOperator, PublicRelations];
}

/// <summary>D-148 — Gate Module permission names. Per the
/// SIMF-RPM-001 model, an Administrator holds every permission; a
/// <see cref="AppRoles.GateOperator"/> holds only the gate permissions.
/// D-168 (gap doc G5) added the public-relations triad.</summary>
public static class Permissions
{
    public const string GatesManage = "Gates.Manage";
    public const string GatesOperate = "Gates.Operate";
    public const string GatesViewOwnReports = "Gates.ViewOwnReports";

    /// <summary>D-168 (gap doc G5) — manage invitation rows (create / edit /
    /// soft-delete / state override). Granted to Administrator and
    /// <see cref="AppRoles.PublicRelations"/>.</summary>
    public const string InvitationsManage = "Invitations.Manage";

    /// <summary>D-168 (gap doc G5) — view the VIP list (users whose
    /// <c>ProfileType.Name</c> is in <c>{VVIP, VIP, Gold}</c>). Granted
    /// to Administrator and <see cref="AppRoles.PublicRelations"/>.</summary>
    public const string VipsView = "Vips.View";

    /// <summary>D-168 (gap doc G5) — dispatch guest-targeted notifications
    /// (in-app row + queued email) to one or more VIPs. Granted to
    /// Administrator and <see cref="AppRoles.PublicRelations"/>.</summary>
    public const string VipsNotify = "Vips.Notify";
}

/// <summary>D-168 (gap doc G5, PDF §2.7.3) — the VIP discriminator.
/// A <c>UserProfile</c> is a VIP when its <c>ProfileType.Name</c> is
/// one of these values; the VIP list + bulk-notify endpoints filter on
/// this set, and the seed comment on <see cref="AppRoles.PublicRelations"/>
/// references it.</summary>
public static class VipProfileTypes
{
    public const string Vvip = "VVIP";
    public const string Vip = "VIP";
    public const string Gold = "Gold";

    /// <summary>Every name in the VIP set.</summary>
    public static readonly IReadOnlyList<string> All = [Vvip, Vip, Gold];
}
