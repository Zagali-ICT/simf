namespace SIMF.Common;

/// <summary>
/// One entry in the permission catalogue — the metadata seeded into the
/// <c>Permission</c> table (SIMF-RPM-001 §8, SIMF-DAT-001 §5.1).
/// <para><paramref name="BaselineRoles"/> lists the built-in non-Administrator
/// roles that receive this permission as a seeded grant. Administrator is
/// never listed: it is resolved to the wildcard <see cref="PermissionCatalog.Wildcard"/>
/// at token-mint time and therefore holds every permission implicitly.</para>
/// </summary>
public sealed record PermissionDef(
    string Code,
    string Page,
    string Action,
    string DisplayName,
    IReadOnlyList<string> BaselineRoles);

/// <summary>
/// The complete page-and-action permission catalogue for the Control Panel
/// and admin API (SIMF-RPM-001 §8). This is the single source of truth:
/// <list type="bullet">
/// <item>endpoints gate with <see cref="PolicyFor"/>(code);</item>
/// <item>CP pages gate with <c>[RequirePermission(code)]</c>;</item>
/// <item>the side menu filters items by their <c>RequiredPermission</c>;</item>
/// <item>the seeder inserts one <c>Permission</c> row per <see cref="All"/> entry.</item>
/// </list>
/// <para>Codes follow the existing <c>Page.Action</c> convention (e.g.
/// <c>Sessions.Edit</c>). The six codes that pre-date this catalogue
/// (D-148 gate triad, D-168 PR/VIP triad) keep their exact strings,
/// pages, actions and display names so existing seeded rows and grants
/// match on re-seed.</para>
/// </summary>
public static class PermissionCatalog
{
    /// <summary>The wildcard permission value an Administrator carries in
    /// its <c>perm</c> claim. A permission check passes when the principal
    /// holds the requested code <b>or</b> this wildcard.</summary>
    public const string Wildcard = "*";

    /// <summary>The JWT / cookie claim type that carries permission codes.</summary>
    public const string ClaimType = "perm";

    /// <summary>The prefix that marks an authorization policy name as a
    /// permission policy (consumed by the dynamic policy provider).</summary>
    public const string PolicyPrefix = "perm:";

    /// <summary>Builds the authorization policy name for a permission code,
    /// e.g. <c>Sessions.Edit</c> → <c>perm:Sessions.Edit</c>. Used by API
    /// endpoints (<c>Policies(PermissionCatalog.PolicyFor(...))</c>).</summary>
    public static string PolicyFor(string code) => PolicyPrefix + code;

    /// <summary>True when an authorization policy name is a permission policy.</summary>
    public static bool IsPermissionPolicy(string policyName) =>
        policyName.StartsWith(PolicyPrefix, StringComparison.Ordinal);

    /// <summary>Extracts the permission code from a permission policy name;
    /// <c>perm:Sessions.Edit</c> → <c>Sessions.Edit</c>.</summary>
    public static string CodeFromPolicy(string policyName) =>
        policyName[PolicyPrefix.Length..];

    // ── People & accounts ────────────────────────────────────────────────

    /// <summary>Admin (CP staff) accounts.</summary>
    public static class Admins
    {
        public const string View = "Admins.View";
        public const string Create = "Admins.Create";
        public const string Delete = "Admins.Delete";
        public const string Approve = "Admins.Approve";
        public const string Reject = "Admins.Reject";
        public const string Export = "Admins.Export";
        public const string Import = "Admins.Import";
        public const string ResetTwoFactor = "Admins.ResetTwoFactor";
        public const string AssignRoles = "Admins.AssignRoles";
    }

    /// <summary>"Other" (partner-side) accounts — Staff / Media / Sponsor.</summary>
    public static class Others
    {
        public const string View = "Others.View";
        public const string Create = "Others.Create";
        public const string Edit = "Others.Edit";
        public const string Delete = "Others.Delete";
        public const string Approve = "Others.Approve";
        public const string Reject = "Others.Reject";
        public const string Export = "Others.Export";
        public const string Import = "Others.Import";
        public const string RegisterOnsite = "Others.RegisterOnsite";
    }

    /// <summary>Visitor (audience) accounts.</summary>
    public static class Visitors
    {
        public const string View = "Visitors.View";
        public const string Create = "Visitors.Create";
        public const string Edit = "Visitors.Edit";
        public const string Delete = "Visitors.Delete";
        public const string Approve = "Visitors.Approve";
        public const string Reject = "Visitors.Reject";
        public const string Export = "Visitors.Export";
        public const string Import = "Visitors.Import";
        public const string RegisterOnsite = "Visitors.RegisterOnsite";
    }

    /// <summary>Attendee roster + badge printing.</summary>
    public static class Attendees
    {
        public const string View = "Attendees.View";
        public const string PrintBag = "Attendees.PrintBag";

        // P1.6 — XLSX export of the (filtered) attendee roster.
        public const string Export = "Attendees.Export";
    }

    /// <summary>RBAC roles and their permission grants.</summary>
    public static class Roles
    {
        public const string View = "Roles.View";
        public const string Create = "Roles.Create";
        public const string Edit = "Roles.Edit";
        public const string Delete = "Roles.Delete";
        public const string AssignPermissions = "Roles.AssignPermissions";
    }

    /// <summary>Profile interests reference data.</summary>
    public static class Interests
    {
        public const string View = "Interests.View";
        public const string Create = "Interests.Create";
        public const string Edit = "Interests.Edit";
        public const string Delete = "Interests.Delete";
    }

    /// <summary>Countries reference data.</summary>
    public static class Countries
    {
        public const string View = "Countries.View";
        public const string Create = "Countries.Create";
        public const string Edit = "Countries.Edit";
        public const string Delete = "Countries.Delete";
    }

    /// <summary>Profile types (visitor + other) reference data.</summary>
    public static class ProfileTypes
    {
        public const string View = "ProfileTypes.View";
        public const string Create = "ProfileTypes.Create";
        public const string Edit = "ProfileTypes.Edit";
        public const string Delete = "ProfileTypes.Delete";
    }

    // ── Programme ────────────────────────────────────────────────────────

    public static class Themes
    {
        public const string View = "Themes.View";
        public const string Create = "Themes.Create";
        public const string Edit = "Themes.Edit";
        public const string Delete = "Themes.Delete";
    }

    public static class Sessions
    {
        public const string View = "Sessions.View";
        public const string Create = "Sessions.Create";
        public const string Edit = "Sessions.Edit";
        public const string Delete = "Sessions.Delete";
    }

    public static class ProgrammeTimeline
    {
        public const string View = "ProgrammeTimeline.View";
    }

    public static class Halls
    {
        public const string View = "Halls.View";
        public const string Create = "Halls.Create";
        public const string Edit = "Halls.Edit";
        public const string Delete = "Halls.Delete";
    }

    /// <summary>Hall seat-layout editor.</summary>
    public static class SeatLayouts
    {
        public const string View = "SeatLayouts.View";
        public const string Edit = "SeatLayouts.Edit";
    }

    /// <summary>Per-session seat plan / reservations.</summary>
    public static class SeatPlans
    {
        public const string View = "SeatPlans.View";
        public const string Edit = "SeatPlans.Edit";
    }

    public static class Delegations
    {
        public const string View = "Delegations.View";
        public const string Create = "Delegations.Create";
        public const string Edit = "Delegations.Edit";
        public const string Delete = "Delegations.Delete";
    }

    public static class MeetingRequests
    {
        public const string View = "MeetingRequests.View";
        public const string Manage = "MeetingRequests.Manage";
    }

    public static class Speakers
    {
        public const string View = "Speakers.View";
        public const string Create = "Speakers.Create";
        public const string Edit = "Speakers.Edit";
        public const string Delete = "Speakers.Delete";
    }

    /// <summary>Assigning moderators to sessions.</summary>
    public static class SessionModerators
    {
        public const string View = "SessionModerators.View";
        public const string Assign = "SessionModerators.Assign";
        public const string Revoke = "SessionModerators.Revoke";
    }

    /// <summary>The live session moderation desk (Q&amp;A + comments).</summary>
    public static class SessionModeration
    {
        public const string Moderate = "SessionModeration.Moderate";
    }

    // ── Exhibition ───────────────────────────────────────────────────────

    public static class Companies
    {
        public const string View = "Companies.View";
        public const string Create = "Companies.Create";
        public const string Edit = "Companies.Edit";
        public const string Delete = "Companies.Delete";
    }

    public static class Booths
    {
        public const string View = "Booths.View";
        public const string Create = "Booths.Create";
        public const string Edit = "Booths.Edit";
        public const string Delete = "Booths.Delete";
    }

    public static class Sponsors
    {
        public const string View = "Sponsors.View";
        public const string Create = "Sponsors.Create";
        public const string Edit = "Sponsors.Edit";
        public const string Delete = "Sponsors.Delete";
    }

    // ── Engagement ───────────────────────────────────────────────────────

    /// <summary>Audience comment moderation.</summary>
    public static class Comments
    {
        public const string View = "Comments.View";
        public const string Moderate = "Comments.Moderate";
    }

    /// <summary>Ratings / feedback viewer.</summary>
    public static class Ratings
    {
        public const string View = "Ratings.View";
    }

    // ── Knowledge ────────────────────────────────────────────────────────

    public static class AiPrompts
    {
        public const string View = "AiPrompts.View";
        public const string Create = "AiPrompts.Create";
        public const string Edit = "AiPrompts.Edit";
        public const string Delete = "AiPrompts.Delete";
        public const string Test = "AiPrompts.Test";
    }

    public static class AiInvocations
    {
        public const string View = "AiInvocations.View";
    }

    // ── Content ──────────────────────────────────────────────────────────

    /// <summary>Dynamic CMS content blocks (upsert covers create + edit).</summary>
    public static class ContentBlocks
    {
        public const string View = "ContentBlocks.View";
        public const string Edit = "ContentBlocks.Edit";
        public const string Delete = "ContentBlocks.Delete";
    }

    public static class Banners
    {
        public const string View = "Banners.View";
        public const string Create = "Banners.Create";
        public const string Edit = "Banners.Edit";
        public const string Delete = "Banners.Delete";
    }

    public static class Media
    {
        public const string View = "Media.View";
        public const string Create = "Media.Create";
        public const string Edit = "Media.Edit";
        public const string Delete = "Media.Delete";
    }

    public static class News
    {
        public const string View = "News.View";
        public const string Create = "News.Create";
        public const string Edit = "News.Edit";
        public const string Delete = "News.Delete";
    }

    public static class MediaPartners
    {
        public const string View = "MediaPartners.View";
        public const string Create = "MediaPartners.Create";
        public const string Edit = "MediaPartners.Edit";
        public const string Delete = "MediaPartners.Delete";
    }

    public static class Archive
    {
        public const string View = "Archive.View";
        public const string Create = "Archive.Create";
        public const string Edit = "Archive.Edit";
        public const string Delete = "Archive.Delete";
    }

    // ── System & operations ──────────────────────────────────────────────

    public static class Statistics
    {
        public const string View = "Statistics.View";
    }

    /// <summary>Gate Module (D-148). <see cref="Manage"/>, <see cref="Operate"/>
    /// and <see cref="ViewOwnReports"/> pre-date this catalogue and keep their
    /// exact strings.</summary>
    public static class Gates
    {
        public const string Manage = "Gates.Manage";
        public const string Operate = "Gates.Operate";
        public const string ViewOwnReports = "Gates.ViewOwnReports";
    }

    /// <summary>Operations toggles (registration / archive visibility).</summary>
    public static class Operations
    {
        public const string View = "Operations.View";
        public const string Edit = "Operations.Edit";
    }

    public static class OperationLog
    {
        public const string View = "OperationLog.View";

        // P1.6 — XLSX export of the (filtered) operation log.
        public const string Export = "OperationLog.Export";
    }

    public static class Logs
    {
        public const string View = "Logs.View";
    }

    /// <summary>Invitations (D-168). <see cref="Manage"/> pre-dates this
    /// catalogue and keeps its exact string.</summary>
    public static class Invitations
    {
        public const string View = "Invitations.View";
        public const string Manage = "Invitations.Manage";
    }

    /// <summary>VIP list + bulk notify (D-168). Both codes pre-date this
    /// catalogue and keep their exact strings.</summary>
    public static class Vips
    {
        public const string View = "Vips.View";
        public const string Notify = "Vips.Notify";
    }

    // These baseline-role lists MUST be declared before `All`: static field
    // initializers run in textual order, and `BuildAll()` reads them — declaring
    // them after `All` would capture their null defaults (seeding every entry
    // with null BaselineRoles, which then NREs the seeder's grant loop).
    private static readonly IReadOnlyList<string> AdminOnly = [];
    private static readonly IReadOnlyList<string> GateOperator = [AppRoles.GateOperator];
    private static readonly IReadOnlyList<string> PublicRelations = [AppRoles.PublicRelations];

    /// <summary>Every permission in the catalogue, in display order. The
    /// seeder inserts one row per entry (idempotent by <see cref="PermissionDef.Code"/>).</summary>
    public static readonly IReadOnlyList<PermissionDef> All = BuildAll();

    private static List<PermissionDef> BuildAll() =>
    [
        // People & accounts
        new(Admins.View, "Admins", "View", "View admin accounts", AdminOnly),
        new(Admins.Create, "Admins", "Create", "Create admin accounts", AdminOnly),
        new(Admins.Delete, "Admins", "Delete", "Delete admin accounts", AdminOnly),
        new(Admins.Approve, "Admins", "Approve", "Approve pending admins", AdminOnly),
        new(Admins.Reject, "Admins", "Reject", "Reject pending admins", AdminOnly),
        new(Admins.Export, "Admins", "Export", "Export admins", AdminOnly),
        new(Admins.Import, "Admins", "Import", "Import admins", AdminOnly),
        new(Admins.ResetTwoFactor, "Admins", "ResetTwoFactor", "Reset a user's two-factor", AdminOnly),
        new(Admins.AssignRoles, "Admins", "AssignRoles", "Assign roles to admins", AdminOnly),

        new(Others.View, "Others", "View", "View other accounts", AdminOnly),
        new(Others.Create, "Others", "Create", "Create other accounts", AdminOnly),
        new(Others.Edit, "Others", "Edit", "Edit other accounts", AdminOnly),
        new(Others.Delete, "Others", "Delete", "Delete other accounts", AdminOnly),
        new(Others.Approve, "Others", "Approve", "Approve pending others", AdminOnly),
        new(Others.Reject, "Others", "Reject", "Reject pending others", AdminOnly),
        new(Others.Export, "Others", "Export", "Export other accounts", AdminOnly),
        new(Others.Import, "Others", "Import", "Import other accounts", AdminOnly),
        new(Others.RegisterOnsite, "Others", "RegisterOnsite", "Walk-in register an other account", AdminOnly),

        new(Visitors.View, "Visitors", "View", "View visitor accounts", AdminOnly),
        new(Visitors.Create, "Visitors", "Create", "Create visitor accounts", AdminOnly),
        new(Visitors.Edit, "Visitors", "Edit", "Edit visitor accounts", AdminOnly),
        new(Visitors.Delete, "Visitors", "Delete", "Delete visitor accounts", AdminOnly),
        new(Visitors.Approve, "Visitors", "Approve", "Approve pending visitors", AdminOnly),
        new(Visitors.Reject, "Visitors", "Reject", "Reject pending visitors", AdminOnly),
        new(Visitors.Export, "Visitors", "Export", "Export visitors", AdminOnly),
        new(Visitors.Import, "Visitors", "Import", "Import visitors", AdminOnly),
        new(Visitors.RegisterOnsite, "Visitors", "RegisterOnsite", "Walk-in register a visitor", AdminOnly),

        new(Attendees.View, "Attendees", "View", "View the attendee roster", AdminOnly),
        new(Attendees.PrintBag, "Attendees", "PrintBag", "Print attendee badges", AdminOnly),
        new(Attendees.Export, "Attendees", "Export", "Export the attendee roster", AdminOnly),

        new(Roles.View, "Roles", "View", "View roles", AdminOnly),
        new(Roles.Create, "Roles", "Create", "Create roles", AdminOnly),
        new(Roles.Edit, "Roles", "Edit", "Rename roles", AdminOnly),
        new(Roles.Delete, "Roles", "Delete", "Delete roles", AdminOnly),
        new(Roles.AssignPermissions, "Roles", "AssignPermissions", "Assign permissions to roles", AdminOnly),

        new(Interests.View, "Interests", "View", "View interests", AdminOnly),
        new(Interests.Create, "Interests", "Create", "Create interests", AdminOnly),
        new(Interests.Edit, "Interests", "Edit", "Edit interests", AdminOnly),
        new(Interests.Delete, "Interests", "Delete", "Delete interests", AdminOnly),

        new(Countries.View, "Countries", "View", "View countries", AdminOnly),
        new(Countries.Create, "Countries", "Create", "Create countries", AdminOnly),
        new(Countries.Edit, "Countries", "Edit", "Edit countries", AdminOnly),
        new(Countries.Delete, "Countries", "Delete", "Delete countries", AdminOnly),

        new(ProfileTypes.View, "ProfileTypes", "View", "View profile types", AdminOnly),
        new(ProfileTypes.Create, "ProfileTypes", "Create", "Create profile types", AdminOnly),
        new(ProfileTypes.Edit, "ProfileTypes", "Edit", "Edit profile types", AdminOnly),
        new(ProfileTypes.Delete, "ProfileTypes", "Delete", "Delete profile types", AdminOnly),

        // Programme
        new(Themes.View, "Themes", "View", "View themes", AdminOnly),
        new(Themes.Create, "Themes", "Create", "Create themes", AdminOnly),
        new(Themes.Edit, "Themes", "Edit", "Edit themes", AdminOnly),
        new(Themes.Delete, "Themes", "Delete", "Delete themes", AdminOnly),

        new(Sessions.View, "Sessions", "View", "View sessions", AdminOnly),
        new(Sessions.Create, "Sessions", "Create", "Create sessions", AdminOnly),
        new(Sessions.Edit, "Sessions", "Edit", "Edit sessions", AdminOnly),
        new(Sessions.Delete, "Sessions", "Delete", "Delete sessions", AdminOnly),

        new(ProgrammeTimeline.View, "ProgrammeTimeline", "View", "View the programme timeline", AdminOnly),

        new(Halls.View, "Halls", "View", "View halls", AdminOnly),
        new(Halls.Create, "Halls", "Create", "Create halls", AdminOnly),
        new(Halls.Edit, "Halls", "Edit", "Edit halls", AdminOnly),
        new(Halls.Delete, "Halls", "Delete", "Delete halls", AdminOnly),

        new(SeatLayouts.View, "SeatLayouts", "View", "View hall seat layouts", AdminOnly),
        new(SeatLayouts.Edit, "SeatLayouts", "Edit", "Edit hall seat layouts", AdminOnly),

        new(SeatPlans.View, "SeatPlans", "View", "View session seat plans", AdminOnly),
        new(SeatPlans.Edit, "SeatPlans", "Edit", "Edit session seat plans", AdminOnly),

        new(Delegations.View, "Delegations", "View", "View delegations", AdminOnly),
        new(Delegations.Create, "Delegations", "Create", "Create delegations", AdminOnly),
        new(Delegations.Edit, "Delegations", "Edit", "Edit delegations", AdminOnly),
        new(Delegations.Delete, "Delegations", "Delete", "Delete delegations", AdminOnly),

        new(MeetingRequests.View, "MeetingRequests", "View", "View meeting requests", AdminOnly),
        new(MeetingRequests.Manage, "MeetingRequests", "Manage", "Manage meeting requests", AdminOnly),

        new(Speakers.View, "Speakers", "View", "View speakers", AdminOnly),
        new(Speakers.Create, "Speakers", "Create", "Create speakers", AdminOnly),
        new(Speakers.Edit, "Speakers", "Edit", "Edit speakers", AdminOnly),
        new(Speakers.Delete, "Speakers", "Delete", "Delete speakers", AdminOnly),

        new(SessionModerators.View, "SessionModerators", "View", "View session moderators", AdminOnly),
        new(SessionModerators.Assign, "SessionModerators", "Assign", "Assign session moderators", AdminOnly),
        new(SessionModerators.Revoke, "SessionModerators", "Revoke", "Revoke session moderators", AdminOnly),

        new(SessionModeration.Moderate, "SessionModeration", "Moderate", "Moderate a live session", AdminOnly),

        // Exhibition
        new(Companies.View, "Companies", "View", "View companies", AdminOnly),
        new(Companies.Create, "Companies", "Create", "Create companies", AdminOnly),
        new(Companies.Edit, "Companies", "Edit", "Edit companies", AdminOnly),
        new(Companies.Delete, "Companies", "Delete", "Delete companies", AdminOnly),

        new(Booths.View, "Booths", "View", "View booths", AdminOnly),
        new(Booths.Create, "Booths", "Create", "Create booths", AdminOnly),
        new(Booths.Edit, "Booths", "Edit", "Edit booths", AdminOnly),
        new(Booths.Delete, "Booths", "Delete", "Delete booths", AdminOnly),

        new(Sponsors.View, "Sponsors", "View", "View sponsors", AdminOnly),
        new(Sponsors.Create, "Sponsors", "Create", "Create sponsors", AdminOnly),
        new(Sponsors.Edit, "Sponsors", "Edit", "Edit sponsors", AdminOnly),
        new(Sponsors.Delete, "Sponsors", "Delete", "Delete sponsors", AdminOnly),

        // Engagement
        new(Comments.View, "Comments", "View", "View audience comments", AdminOnly),
        new(Comments.Moderate, "Comments", "Moderate", "Moderate audience comments", AdminOnly),

        new(Ratings.View, "Ratings", "View", "View ratings and feedback", AdminOnly),

        // Knowledge
        new(AiPrompts.View, "AiPrompts", "View", "View AI prompts", AdminOnly),
        new(AiPrompts.Create, "AiPrompts", "Create", "Create AI prompts", AdminOnly),
        new(AiPrompts.Edit, "AiPrompts", "Edit", "Edit AI prompts", AdminOnly),
        new(AiPrompts.Delete, "AiPrompts", "Delete", "Delete AI prompts", AdminOnly),
        new(AiPrompts.Test, "AiPrompts", "Test", "Test AI prompts", AdminOnly),

        new(AiInvocations.View, "AiInvocations", "View", "View AI invocations log", AdminOnly),

        // Content
        new(ContentBlocks.View, "ContentBlocks", "View", "View content blocks", AdminOnly),
        new(ContentBlocks.Edit, "ContentBlocks", "Edit", "Edit content blocks", AdminOnly),
        new(ContentBlocks.Delete, "ContentBlocks", "Delete", "Delete content blocks", AdminOnly),

        new(Banners.View, "Banners", "View", "View banners", AdminOnly),
        new(Banners.Create, "Banners", "Create", "Create banners", AdminOnly),
        new(Banners.Edit, "Banners", "Edit", "Edit banners", AdminOnly),
        new(Banners.Delete, "Banners", "Delete", "Delete banners", AdminOnly),

        new(Media.View, "Media", "View", "View media gallery", AdminOnly),
        new(Media.Create, "Media", "Create", "Create media items", AdminOnly),
        new(Media.Edit, "Media", "Edit", "Edit media items", AdminOnly),
        new(Media.Delete, "Media", "Delete", "Delete media items", AdminOnly),

        // News is PR/marketing territory: the admin News endpoints were gated
        // by PublicRelationsAccess before this catalogue, so the PublicRelations
        // role keeps News as a seeded baseline grant (preserves prior behaviour).
        new(News.View, "News", "View", "View news articles", PublicRelations),
        new(News.Create, "News", "Create", "Create news articles", PublicRelations),
        new(News.Edit, "News", "Edit", "Edit news articles", PublicRelations),
        new(News.Delete, "News", "Delete", "Delete news articles", PublicRelations),

        new(MediaPartners.View, "MediaPartners", "View", "View media partners", AdminOnly),
        new(MediaPartners.Create, "MediaPartners", "Create", "Create media partners", AdminOnly),
        new(MediaPartners.Edit, "MediaPartners", "Edit", "Edit media partners", AdminOnly),
        new(MediaPartners.Delete, "MediaPartners", "Delete", "Delete media partners", AdminOnly),

        new(Archive.View, "Archive", "View", "View archive editions", AdminOnly),
        new(Archive.Create, "Archive", "Create", "Create archive editions", AdminOnly),
        new(Archive.Edit, "Archive", "Edit", "Edit archive editions", AdminOnly),
        new(Archive.Delete, "Archive", "Delete", "Delete archive editions", AdminOnly),

        // System & operations
        new(Statistics.View, "Statistics", "View", "View the statistics dashboard", AdminOnly),

        new(Gates.Manage, "Gates", "Manage", "Manage gates", AdminOnly),
        new(Gates.Operate, "Gates", "Operate", "Operate a gate", GateOperator),
        new(Gates.ViewOwnReports, "Gates", "ViewOwnReports", "View own gate reports", GateOperator),

        new(Operations.View, "Operations", "View", "View operations toggles", AdminOnly),
        new(Operations.Edit, "Operations", "Edit", "Change operations toggles", AdminOnly),

        new(OperationLog.View, "OperationLog", "View", "View the operation log", AdminOnly),
        new(OperationLog.Export, "OperationLog", "Export", "Export the operation log", AdminOnly),
        new(Logs.View, "Logs", "View", "View system logs", AdminOnly),

        new(Invitations.View, "Invitations", "View", "View invitations", PublicRelations),
        new(Invitations.Manage, "Invitations", "Manage", "Manage invitations", PublicRelations),

        new(Vips.View, "Vips", "View", "View the VIP list", PublicRelations),
        new(Vips.Notify, "Vips", "Notify", "Notify VIPs", PublicRelations),
    ];
}
