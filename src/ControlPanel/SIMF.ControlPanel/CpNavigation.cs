// Tests: SIMF.ControlPanel.Tests/CpNavigationTests.cs
using SIMF.Common;

namespace SIMF.ControlPanel;

/// <summary>
/// The Control Panel navigation map: the groups and their module links. Labels
/// are resource keys resolved through <see cref="Strings" />. The shell hides
/// items whose <see cref="NavItem.RequiredPermission"/> the signed-in user
/// lacks (Administrator's wildcard shows everything).
/// </summary>
public static class CpNavigation
{
    /// <summary>One module link. <c>IsStub</c> marks entries that still resolve
    /// to <c>ModulePlaceholder</c> and renders a "Soon" badge.
    /// <c>RequiredPermission</c> gates the item in the side menu; <c>null</c>
    /// means visible to any signed-in user. <c>Icon</c> is a <c>SimfIcon</c> key
    /// (sub-menu items only).</summary>
    public sealed record NavItem(
        string LabelKey, string Href, bool IsStub = false, string? RequiredPermission = null,
        string? Icon = null)
    {
        /// <summary>The single source of truth for nav visibility, shared by the
        /// side menu (<c>CpShellLayout</c>) and the help-assistant directory
        /// (<c>CpAssistantDirectory</c>).</summary>
        public bool IsPermittedFor(
            IReadOnlySet<string> permissions, bool hasAllPermissions) =>
            RequiredPermission is null
            || hasAllPermissions
            || permissions.Contains(RequiredPermission);
    }

    /// <summary>One navigation group — a heading and its items.</summary>
    public sealed record NavGroup(string LabelKey, IReadOnlyList<NavItem> Items);

    /// <summary>The navigation groups, in display order.</summary>
    public static readonly IReadOnlyList<NavGroup> Groups =
    [
        new("Nav.Overview",
        [
            new("Module.Dashboard", "/", Icon: "layout-dashboard"),
            // FR-506.
            new("Module.Attendance", "/admin/attendance", RequiredPermission: PermissionCatalog.Attendance.View, Icon: "user-check"),
            new("Module.SessionLiveHall", "/admin/sessions/live-hall", RequiredPermission: PermissionCatalog.Attendance.View, Icon: "monitor"),
            // The icon is "bar-chart", NOT "chart-bar". SimfIcon throws on an
            // unknown name and this nav renders on every page, so the transposed
            // name once broke 92 of 97 pages. CpNavigationIconTests pins every
            // nav icon to the set SimfIcon knows.
            new("Module.Statistics", "/admin/statistics", RequiredPermission: PermissionCatalog.Statistics.View, Icon: "bar-chart"),
        ]),
        new("Nav.People",
        [
            new("Module.Attendees", "/admin/attendees", RequiredPermission: PermissionCatalog.Attendees.View, Icon: "users"),
            new("Module.AdminVisitors", "/admin/visitors", RequiredPermission: PermissionCatalog.Visitors.View, Icon: "user"),
            new("Module.AdminVisitorsPending", "/admin/visitors/pending", RequiredPermission: PermissionCatalog.Visitors.View, Icon: "hourglass"),
            new("Module.AdminVisitorsVip", "/admin/visitors/vip", RequiredPermission: PermissionCatalog.Visitors.RegisterOnsite, Icon: "star"),
            new("Module.AdminVisitorsVipExport", "/admin/visitors/vip/export", RequiredPermission: PermissionCatalog.Visitors.ExportVip, Icon: "download"),
            new("Module.AdminDelegates", "/admin/delegates", RequiredPermission: PermissionCatalog.Visitors.RegisterOnsite, Icon: "users"),
            new("Module.AdminBadgeBatches", "/admin/visitors/badge-batches", RequiredPermission: PermissionCatalog.Visitors.ViewBatches, Icon: "layers"),
            new("Module.AdminOthers", "/admin/others", RequiredPermission: PermissionCatalog.Others.View, Icon: "id-card"),
            new("Module.AdminOthersPending", "/admin/others/pending", RequiredPermission: PermissionCatalog.Others.View, Icon: "hourglass"),
            new("Module.PrintBag", "/admin/print-bag", RequiredPermission: PermissionCatalog.Attendees.PrintBag, Icon: "printer"),
        ]),
        new("Nav.AccessControl",
        [
            new("Module.AdminAdmins", "/admin/admins", RequiredPermission: PermissionCatalog.Admins.View, Icon: "shield"),
            new("Module.AdminAdminsPending", "/admin/admins/pending", RequiredPermission: PermissionCatalog.Admins.View, Icon: "hourglass"),
            new("Module.Roles", "/admin/roles", RequiredPermission: PermissionCatalog.Roles.View, Icon: "key"),
            new("Module.AdminResetTwoFactor", "/admin/reset-2fa", RequiredPermission: PermissionCatalog.Admins.ResetTwoFactor, Icon: "rotate"),
        ]),
        new("Nav.Programme",
        [
            new("Module.Themes", "/admin/themes", RequiredPermission: PermissionCatalog.Themes.View, Icon: "layers"),
            new("Module.Sessions", "/admin/sessions", RequiredPermission: PermissionCatalog.Sessions.View, Icon: "calendar"),
            new("Module.SessionCategories", "/admin/session-categories", RequiredPermission: PermissionCatalog.SessionCategories.View, Icon: "folder"),
            new("Module.ProgrammeDays", "/admin/programme-days", RequiredPermission: PermissionCatalog.ProgrammeDays.View, Icon: "calendar"),
            new("Module.ProgrammeTimeline", "/admin/programme/timeline", RequiredPermission: PermissionCatalog.ProgrammeTimeline.View, Icon: "clock"),
            new("Module.Halls", "/admin/halls", RequiredPermission: PermissionCatalog.Halls.View, Icon: "building"),
            new("Module.HallSeatLayouts", "/admin/halls/seat-layouts", RequiredPermission: PermissionCatalog.SeatLayouts.View, Icon: "grid"),
            // There is deliberately no page for the per-hall arrival boundary:
            // HallsAddEdit.razor already edits the geofence fields on the hall.
            new("Module.SessionSeatPlans", "/admin/sessions/seat-plans", RequiredPermission: PermissionCatalog.SeatPlans.View, Icon: "armchair"),
            new("Module.SpeakerMeetingRequests", "/admin/speaker-meeting-requests", RequiredPermission: PermissionCatalog.SpeakerMeetingRequests.View, Icon: "inbox"),
            new("Module.AdminSpeakerAvailability", "/admin/speaker-availability", RequiredPermission: PermissionCatalog.SpeakerMeetingRequests.Manage, Icon: "calendar"),
            // Hall-scoped: both meeting desks read the slots this produces.
            new("Module.AdminHallAvailability", "/admin/hall-availability", RequiredPermission: PermissionCatalog.HallAvailability.Manage, Icon: "calendar"),
            new("Module.AdminDelegationMeetings", "/admin/delegation-meetings", RequiredPermission: PermissionCatalog.DelegationMeetings.View, Icon: "inbox"),
            new("Module.AdminDelegationAvailability", "/admin/delegation-availability", RequiredPermission: PermissionCatalog.DelegationMeetings.Manage, Icon: "calendar"),
            new("Module.AdminDocumentRequests", "/admin/document-requests", RequiredPermission: PermissionCatalog.ParticipationDocumentRequests.View, Icon: "inbox"),
            new("Module.AdminBadgeRequests", "/admin/badge-requests", RequiredPermission: PermissionCatalog.BadgeUpdateRequests.View, Icon: "inbox"),
            new("Module.Speakers", "/admin/speakers", RequiredPermission: PermissionCatalog.Speakers.View, Icon: "mic"),
            // FR-407.
            new("Module.SpeakerPresentations", "/admin/speaker-presentations", RequiredPermission: PermissionCatalog.Speakers.View, Icon: "presentation"),
            new("Module.Bookings", "/admin/bookings", RequiredPermission: PermissionCatalog.Bookings.View, Icon: "ticket"),
            new("Module.MeetingTables", "/admin/meeting-tables", RequiredPermission: PermissionCatalog.MeetingTables.View, Icon: "table"),
            new("Module.BusinessMeetings", "/admin/business-meetings", RequiredPermission: PermissionCatalog.BusinessMeetings.View, Icon: "handshake"),
        ]),
        new("Nav.ScientificCommittee",
        [
            // The moderator's own live desk is /sessions/{id}/moderate, reached
            // from the Sessions grid rather than from the nav.
            new("Module.SessionModerators", "/admin/session-moderators", RequiredPermission: PermissionCatalog.SessionModerators.View, Icon: "gavel"),
            new("Module.QuestionQueue", "/admin/question-queue", RequiredPermission: PermissionCatalog.Questions.View, Icon: "help-circle"),
            new("Module.SessionSummaries", "/admin/session-summaries", RequiredPermission: PermissionCatalog.SessionSummaries.View, Icon: "file-text"),
        ]),
        new("Nav.Exhibition",
        [
            // In-app exhibitor self-signup was permanently descoped: onboarding
            // is CP-only, through this page plus Booths and Sponsors.
            new("Module.Exhibitors", "/admin/exhibitors", RequiredPermission: PermissionCatalog.Exhibitors.View, Icon: "briefcase"),
            new("Module.Booths", "/admin/booths", RequiredPermission: PermissionCatalog.Booths.View, Icon: "store"),
            new("Module.Sponsors", "/admin/sponsors", RequiredPermission: PermissionCatalog.Sponsors.View, Icon: "award"),
            new("Module.VenueMap", "/admin/venue-map", RequiredPermission: PermissionCatalog.VenueMap.View, Icon: "map"),
        ]),
        new("Nav.Engagement",
        [
            // The last remaining stub. It still carries a real permission,
            // because the "Soon" badge is not a gate: null would advertise it to
            // every signed-in admin regardless of role.
            new("Module.LiveSessions", "/m/live-sessions", IsStub: true, RequiredPermission: PermissionCatalog.Sessions.View, Icon: "video"),
            new("Module.Ratings", "/admin/ratings", RequiredPermission: PermissionCatalog.Ratings.View, Icon: "star"),
            new("Module.RatingConfig", "/admin/rating-config", RequiredPermission: PermissionCatalog.RatingConfig.View, Icon: "sliders"),
        ]),
        new("Nav.Knowledge",
        [
            new("Module.Faq", "/admin/faq", RequiredPermission: PermissionCatalog.Faq.View, Icon: "help-circle"),
            new("Module.AiDashboard", "/admin/ai", RequiredPermission: PermissionCatalog.AiDashboard.View, Icon: "bar-chart"),
            // Aggregates the prompt catalogue by feature, so it shares AiPrompts.View.
            new("Module.AiServices", "/admin/ai/services", RequiredPermission: PermissionCatalog.AiPrompts.View, Icon: "list"),
            new("Module.AiPrompts", "/admin/ai/prompts", RequiredPermission: PermissionCatalog.AiPrompts.View, Icon: "sparkle"),
            new("Module.AiInvocations", "/admin/ai/invocations", RequiredPermission: PermissionCatalog.AiInvocations.View, Icon: "list-tree"),
        ]),
        new("Nav.Content",
        [
            new("Module.MediaLibrary", "/admin/media-library", RequiredPermission: PermissionCatalog.MediaLibrary.View, Icon: "grid"),
            new("Module.ContentBlocks", "/admin/content-blocks", RequiredPermission: PermissionCatalog.ContentBlocks.View, Icon: "layout"),
            new("Module.Banners", "/admin/banners", RequiredPermission: PermissionCatalog.Banners.View, Icon: "image"),
            new("Module.Media", "/admin/media", RequiredPermission: PermissionCatalog.Media.View, Icon: "film"),
            new("Module.News", "/admin/news", RequiredPermission: PermissionCatalog.News.View, Icon: "newspaper"),
            new("Module.MediaPartners", "/admin/media-partners", RequiredPermission: PermissionCatalog.MediaPartners.View, Icon: "share"),
            new("Module.PreviousEditions", "/admin/archive", RequiredPermission: PermissionCatalog.Archive.View, Icon: "archive"),
        ]),
        // Broadcast (admin to audience) is the Announcements desk below. The
        // operator's own notification inbox is /account/notifications, reached
        // from the bell rather than from the nav.
        new("Nav.PublicRelations",
        [
            new("Module.Invitations", "/admin/invitations", RequiredPermission: PermissionCatalog.Invitations.View, Icon: "mail"),
            new("Module.Vips", "/admin/vips", RequiredPermission: PermissionCatalog.Vips.View, Icon: "crown"),
            // NAV-007 — gates on View, matching the page. It gated on Send, so a
            // role granted View alone got no menu item for a page it could open.
            new("Module.Announcements", "/admin/announcements", RequiredPermission: PermissionCatalog.Announcements.View, Icon: "send"),
            new("Module.ContactInquiries", "/admin/contact-inquiries", RequiredPermission: PermissionCatalog.ContactInquiries.View, Icon: "inbox"),
        ]),
        new("Nav.Gates",
        [
            new("Module.Gates", "/admin/gates", RequiredPermission: PermissionCatalog.Gates.Manage, Icon: "door"),
            new("Module.GatesOperator", "/admin/gates/operator", RequiredPermission: PermissionCatalog.Gates.Operate, Icon: "scan"),
            new("Module.HallArrivals", "/admin/hall-arrivals", RequiredPermission: PermissionCatalog.HallArrivals.View, Icon: "log-in"),
            new("Module.GatesDashboard", "/admin/gates/dashboard", RequiredPermission: PermissionCatalog.Gates.Manage, Icon: "bar-chart"),
        ]),
        new("Nav.ReferenceData",
        [
            new("Module.AdminInterests", "/admin/interests", RequiredPermission: PermissionCatalog.Interests.View, Icon: "tag"),
            new("Module.AdminCountries", "/admin/countries", RequiredPermission: PermissionCatalog.Countries.View, Icon: "globe"),
            // Saudi-companies lookup (gov Excel import) behind the visitor الجهة picker.
            new("Module.Organisations", "/admin/organisations", RequiredPermission: PermissionCatalog.Organisations.View, Icon: "building"),
            new("Module.Regions", "/admin/regions", RequiredPermission: PermissionCatalog.Regions.View, Icon: "map"),
            new("Module.AdminVisitorProfileTypes", "/admin/profile-types/visitor", RequiredPermission: PermissionCatalog.ProfileTypes.View, Icon: "list"),
            new("Module.AdminOtherProfileTypes", "/admin/profile-types/other", RequiredPermission: PermissionCatalog.ProfileTypes.View, Icon: "list"),
        ]),
        new("Nav.System",
        [
            new("Module.Configuration", "/admin/configuration", RequiredPermission: PermissionCatalog.Configuration.View, Icon: "settings"),
            // NAV-006 — gates on Edit, matching the page. It gated on View, so a
            // read-only role saw the item and was then bounced by the page.
            new("Module.SiteSettings", "/admin/site-settings", RequiredPermission: PermissionCatalog.Configuration.Edit, Icon: "globe"),
            new("Module.EmailTemplates", "/admin/email/templates", RequiredPermission: PermissionCatalog.EmailTemplates.View, Icon: "mail"),
            new("Module.OrganizationProfile", "/admin/organization-profile", RequiredPermission: PermissionCatalog.OrganizationProfile.View, Icon: "building"),
            new("Module.AdminLogs", "/admin/logs", RequiredPermission: PermissionCatalog.Logs.View, Icon: "file-text"),
            new("Module.ServicesMonitor", "/admin/ops/services", RequiredPermission: PermissionCatalog.ServicesMonitor.View, Icon: "bar-chart"),
            new("Module.OperationLog", "/admin/operation-log", RequiredPermission: PermissionCatalog.OperationLog.View, Icon: "list-tree"),
            new("Module.OperationsToggles", "/admin/operations", RequiredPermission: PermissionCatalog.Operations.View, Icon: "sliders"),
        ]),
        // Each report gates on its own permission, so an operator can be given
        // the gate log without the attendee roster.
        new("Nav.Reports",
        [
            new("Module.ReportsHub", "/admin/reports", RequiredPermission: PermissionCatalog.Reports.View, Icon: "table"),
            new("Module.ReportsAttendance", "/admin/reports/attendance", RequiredPermission: PermissionCatalog.Reports.Attendance, Icon: "user-check"),
            new("Module.ReportsRegistrations", "/admin/reports/registrations", RequiredPermission: PermissionCatalog.Reports.Registrations, Icon: "users"),
            new("Module.ReportsGates", "/admin/reports/gates", RequiredPermission: PermissionCatalog.Reports.Gates, Icon: "shield"),
            new("Module.ReportsSessions", "/admin/reports/sessions", RequiredPermission: PermissionCatalog.Reports.Sessions, Icon: "presentation"),
            new("Module.ReportsRatings", "/admin/reports/ratings", RequiredPermission: PermissionCatalog.Reports.Ratings, Icon: "star"),
            new("Module.ReportsPartners", "/admin/reports/partners", RequiredPermission: PermissionCatalog.Reports.Partners, Icon: "handshake"),
            new("Module.ReportsMeetings", "/admin/reports/meetings", RequiredPermission: PermissionCatalog.Reports.Meetings, Icon: "calendar"),
            new("Module.ReportsEngagement", "/admin/reports/engagement", RequiredPermission: PermissionCatalog.Reports.Engagement, Icon: "message-circle"),
        ]),
    ];

    /// <summary>The label key for a module route, or <c>null</c> if it is not a known module.</summary>
    public static string? LabelKeyForHref(string href)
    {
        foreach (var group in Groups)
        {
            foreach (var item in group.Items)
            {
                if (string.Equals(item.Href, href, StringComparison.OrdinalIgnoreCase))
                {
                    return item.LabelKey;
                }
            }
        }
        return null;
    }
}
