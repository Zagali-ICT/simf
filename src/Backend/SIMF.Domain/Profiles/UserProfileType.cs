using SIMF.Common.Enums;
using SIMF.Domain.Common;

namespace SIMF.Domain.Profiles;

/// <summary>
/// A dynamic subtype assigned to a <see cref="SimfUser"/>. After D-186
/// every non-admin account is <c>UserType.Visitor</c>, and the
/// audience-vs-partner split lives on <see cref="IsForVisitor"/>:
/// <c>IsVisitor = true</c> for audience profile types (VVIP, VIP, Gold,
/// Normal); <c>IsVisitor = false</c> for partner / staff profile types
/// (Staff, Exhibitor, Sponsor, Media). Editable from the CP at runtime
/// — adding a new subtype is a row insert, not a code change.
///
/// <para><see cref="UserType"/> still scopes the picker per surface
/// (Visitor profile types vs the rare Admin-side profile type); within
/// the Visitor scope, <see cref="IsForVisitor"/> decides whether the CP
/// approval queue treats the account as audience or as partner. The CP
/// keeps the two approval pages (Visitors / Others) physically
/// separated to avoid admin confusion during approval; the filter
/// behind each page is <c>UserType = Visitor AND
/// ProfileType.IsVisitor = {true|false}</c>.</para>
///
/// <para><b>Permissions:</b> a profile type grants **no permissions**.
/// It is metadata for display + business rules in the App (which bag,
/// which badge colour, which approval queue). All permission checks
/// key off <see cref="UserType"/> and the user's RBAC roles
/// (Admin only).</para>
/// </summary>
public sealed class UserProfileType : BaseAuditEntity
{
      /// <summary>English display name — "VVIP", "Exhibitor", etc.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Arabic display name — paired with <see cref="Name"/>.</summary>
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>D-186 — audience vs partner split inside the Visitor
    /// scope. <c>true</c> for audience profile types (VVIP, VIP, Gold,
    /// Normal); <c>false</c> for partner / staff profile types
    /// (Sponsor, Exhibitor, Media, Staff) that pre-D-186 lived under
    /// <c>UserType = Other</c>. Defaults to <c>true</c> so a brand-new
    /// profile type is assumed audience-side until an admin says
    /// otherwise. Ignored for Admin-scope profile types (which don't
    /// route through the visitor/other approval queues).</summary>
    public bool IsForVisitor { get; set; } = true;

    /// <summary>
    /// A colour token used by the App (and the CP picker) — a hex
    /// string (e.g. <c>#FFD700</c>) or a CSS variable name. The colour
    /// is the visible bag / badge colour for the event.
    /// </summary>
    public string PageColor { get; set; } = string.Empty;

   

    /// <summary>D-161 — the mobile-app authority any user assigned to this
    /// profile type carries into the Flutter app (SIMF-FDS-002 §8.5).
    /// <see cref="MobileAppRole.None"/> for Visitor-tier profile types and
    /// for Other-tier types with no operational authority (Sponsor,
    /// Speaker, Press, …; D-519: Exhibitor now carries its own role).
    /// Admin-curated at runtime — adding a
    /// new operational profile type is a row insert + an admin checkbox,
    /// not a code change.</summary>
    public MobileAppRole MobileAppRole { get; set; } = MobileAppRole.None;

    /// <summary>D-611 (Wave B) — whether a user of this profile type may book a
    /// VIP speaker-meeting slot. Replaces the former brittle "the profile-type
    /// Name contains 'VIP'" substring test in the meeting-request service, which
    /// would wrongly match any future type whose name merely embedded "VIP".
    /// Admin-curated; the seeder sets it <c>true</c> for the VVIP + VIP rows.</summary>
    public bool AllowsVipMeetingSlots { get; set; }

    /// <summary>D-725 (owner batch item 1) — whether this profile type is
    /// offered to a self-registering user in the mobile app's sign-up picker
    /// (<c>GET /app/account/profile-types</c>). <c>true</c> for audience +
    /// self-serviceable partner types; <c>false</c> for CP-only operational
    /// types (Staff, Moderator) that an admin assigns rather than a customer
    /// self-selecting. Defaults to <c>true</c> so a brand-new type is visible
    /// until an admin hides it. Admin-curated at runtime — a plain checkbox on
    /// the CP profile-type form, not a code change. Does NOT affect CP admin
    /// listings (which always show every type) or any permission — it is a
    /// pure app-registration-picker visibility flag.</summary>
    public bool IsAppRegisterable { get; set; } = true;

    /// <summary>D-760 (owner request) — whether accounts of this profile type
    /// appear in the "Meet People (same interests)" networking surfaces: the
    /// partner directory (<c>GET /app/networking/partner-directory</c>) and the
    /// app's "people like you" recommender. Acts as an admin master switch that
    /// AND-combines with the per-user <c>UserProfile.ShowInMeetLikeYou</c> opt-in:
    /// hiding the type removes ALL its accounts from those surfaces regardless of
    /// the individual opt-in. Defaults to <c>true</c> so existing + freshly
    /// created types keep showing until an admin hides them. Only meaningful for
    /// partner (Other) types — visitor/audience types are curated separately;
    /// the CP exposes the checkbox on the "Others" profile-type form only.</summary>
    public bool ShowInPartnerDirectory { get; set; } = true;

    /// <summary>
    /// D-819 — a small stable number identifying this profile type inside the
    /// offline event badge. <see cref="BaseAuditEntity.Id"/> is a Guid and will
    /// not fit in a QR that has to stay readable when a badge is creased, so the
    /// badge carries this code instead.
    ///
    /// <para>Carrying the type in the badge is what lets a gate decide OFFLINE:
    /// it decrypts the badge, reads the code, and checks it against that gate's
    /// allowed-profile-type list with no lookup and no network.</para>
    ///
    /// <para>Assigned once and never reused, because badges already printed
    /// under a code stay in circulation. 0 means "not assigned", which no badge
    /// can carry.</para>
    /// </summary>
    public short Code { get; set; }
}
