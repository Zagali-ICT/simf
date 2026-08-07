namespace SIMF.Common;

/// <summary>
/// The Control Panel **RBAC role** names. RBAC roles apply **only** to users
/// with <c>UserType = Admin</c>; <c>Visitor</c> accounts never carry an RBAC
/// role — their kind comes from the <c>UserType</c> column and their subtype
/// from <c>ProfileType</c>, which is also what tells the audience side apart
/// from the partner side.
///
/// <para>The active CP roles are <see cref="Administrator"/>,
/// <see cref="GateOperator"/>, <see cref="PublicRelations"/>,
/// <see cref="SecurityTeam"/> and <see cref="ScientificCommittee"/>. Future
/// fine-grained Admin-side roles (e.g. <c>AuditViewer</c>,
/// <c>RegistrationApprover</c>) plug in here.</para>
///
/// <para>The old "reviewer roles" (Staff / Scientific / Security) were
/// removed as *reviewer kinds* — those are now <c>ProfileType</c> rows
/// (the app-side reviewer subtype), not RBAC roles. The
/// <see cref="SecurityTeam"/> and <see cref="ScientificCommittee"/> roles
/// added here are a different concept: they are CP-side RBAC **permission
/// sets** (the "Security team" and "Scientific team" Control-Panel access
/// bundles) carrying seeded baseline permission grants, exactly like
/// <see cref="GateOperator"/> and <see cref="PublicRelations"/> — not
/// app-side profile subtypes.</para>
/// </summary>
public static class AppRoles
{
    public const string Administrator = "Administrator";

    /// <summary>Operator role for the Gate Module. Holders carry
    /// the <see cref="Permissions.GatesOperate"/> and
    /// <see cref="Permissions.GatesViewOwnReports"/> permissions. Operators
    /// authenticate against the CP surface (they use the operator console).</summary>
    public const string GateOperator = "GateOperator";

    /// <summary>The public-relations team role. Holders manage
    /// <c>Invitation</c> rows, view the VIP list, and dispatch guest-targeted
    /// notifications. PublicRelations shares the existing CP "System" layout
    /// group rather than gaining its own layout — this can be split later if
    /// the team grows past one shared page set.</summary>
    public const string PublicRelations = "PublicRelations";

    /// <summary>The Security team CP permission set. Holders manage the
    /// access-control surface: the gates (<c>Gates.*</c>), the hall-door arrival
    /// console (<c>HallArrivals.*</c>) and the session-attendance dashboard
    /// (<c>Attendance.View</c>). Its baseline grants sit alongside
    /// <see cref="GateOperator"/> on the shared gate codes. A CP-side RBAC
    /// permission set, NOT the removed "Security" reviewer ProfileType.</summary>
    public const string SecurityTeam = "SecurityTeam";

    /// <summary>The Scientific team CP permission set. Holders run the
    /// scientific-programme surface: sessions (<c>Sessions.*</c>), the Q&amp;A /
    /// moderation queue (<c>Questions.*</c>, <c>SessionModeration.Moderate</c>),
    /// the AI محضر / session-summary desk (<c>SessionSummaries.*</c>), ratings
    /// (<c>Ratings.*</c>), speakers (<c>Speakers.*</c>) and the programme-days
    /// manager (<c>ProgrammeDays.*</c>). This makes the "Scientific Committee"
    /// bundle a first-class seeded role instead of one the owner hand-assembles
    /// in the grant editor. A CP-side RBAC permission set, NOT the removed
    /// "Scientific" reviewer ProfileType.</summary>
    public const string ScientificCommittee = "ScientificCommittee";

    /// <summary>Every CP-side RBAC role.</summary>
    public static readonly IReadOnlyList<string> CpRoles =
        [Administrator, GateOperator, PublicRelations, SecurityTeam, ScientificCommittee];
}

/// <summary>Gate Module permission names. An Administrator holds every
/// permission; a <see cref="AppRoles.GateOperator"/> holds only the gate
/// permissions. The public-relations triad lives here too.</summary>
public static class Permissions
{
    public const string GatesManage = "Gates.Manage";
    public const string GatesOperate = "Gates.Operate";
    public const string GatesViewOwnReports = "Gates.ViewOwnReports";

    /// <summary>Manage invitation rows (create / edit /
    /// soft-delete / state override). Granted to Administrator and
    /// <see cref="AppRoles.PublicRelations"/>.</summary>
    public const string InvitationsManage = "Invitations.Manage";

    /// <summary>View the VIP list (users whose
    /// <c>ProfileType.Name</c> is in <c>{VVIP, VIP, Gold}</c>). Granted
    /// to Administrator and <see cref="AppRoles.PublicRelations"/>.</summary>
    public const string VipsView = "Vips.View";

    /// <summary>Dispatch guest-targeted notifications
    /// (in-app row + queued email) to one or more VIPs. Granted to
    /// Administrator and <see cref="AppRoles.PublicRelations"/>.</summary>
    public const string VipsNotify = "Vips.Notify";
}

/// <summary>The VIP discriminator. A <c>UserProfile</c> is a VIP when its
/// <c>ProfileType.Name</c> is one of these values; the VIP list + bulk-notify
/// endpoints filter on this set, and the seed comment on
/// <see cref="AppRoles.PublicRelations"/> references it.</summary>
public static class VipProfileTypes
{
    public const string Vvip = "VVIP";
    public const string Vip = "VIP";
    public const string Gold = "Gold";

    /// <summary>Every name in the VIP set.</summary>
    public static readonly IReadOnlyList<string> All = [Vvip, Vip, Gold];
}
