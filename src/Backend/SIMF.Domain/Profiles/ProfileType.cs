using SIMF.Common.Enums;

namespace SIMF.Domain.Profiles;

/// <summary>
/// A dynamic subtype assigned to a <see cref="SimfUser"/> (P7 — decision
/// D-048). Visitors carry one (VVIP / VIP / Gold / … with a bag /
/// page colour); Others carry one (Staff / Exhibitor / Sponsor / Media /
/// … colour for the App badge). Editable from the CP at runtime — adding
/// a new subtype is a row insert, not a code change.
///
/// <para>The lookup is **bilingual** (EN + AR) and **scoped by
/// <see cref="UserType"/></para>: a Visitor's subtype picker filters
/// <c>UserType = Visitor</c>; an Other's picker filters <c>UserType =
/// Other</c>. Admins do not carry a profile type today; the column is
/// retained on this entity to keep one table for any future
/// Admin-side metadata.</para>
///
/// <para><b>Permissions:</b> a profile type grants **no permissions**.
/// It is metadata for display + business rules in the App (which bag,
/// which badge colour). All permission checks key off
/// <see cref="UserType"/> and the user's RBAC roles (Admin only).</para>
/// </summary>
public sealed class ProfileType
{
    public Guid Id { get; set; }

    /// <summary>English display name — "VVIP", "Exhibitor", etc.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Arabic display name — paired with <see cref="Name"/>.</summary>
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>
    /// A colour token used by the App (and the CP picker) — a hex
    /// string (e.g. <c>#FFD700</c>) or a CSS variable name. The colour
    /// is the visible bag / badge colour for the event.
    /// </summary>
    public string PageColor { get; set; } = string.Empty;

    /// <summary>Which <see cref="UserType"/> this profile type applies to.</summary>
    public UserType UserType { get; set; }

    /// <summary>D-161 — the mobile-app authority any user assigned to this
    /// profile type carries into the Flutter app (SIMF-FDS-002 §8.5).
    /// <see cref="MobileAppRole.None"/> for Visitor-tier profile types and
    /// for Other-tier types with no operational authority (Exhibitor,
    /// Sponsor, Speaker, Press, …). Admin-curated at runtime — adding a
    /// new operational profile type is a row insert + an admin checkbox,
    /// not a code change.</summary>
    public MobileAppRole MobileAppRole { get; set; } = MobileAppRole.None;

    /// <summary>Soft-delete flag — false hides the row from pickers.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}
