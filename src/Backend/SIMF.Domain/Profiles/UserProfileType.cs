using SIMF.Common.Enums;
using SIMF.Domain.Common;

namespace SIMF.Domain.Profiles;

/// <summary>An admin-curated subtype (VVIP, Gold, Exhibitor, Staff): display and
/// business-rule metadata only, never a source of permissions.</summary>
public sealed class UserProfileType : BaseAuditEntity
{
    public string Name { get; set; } = string.Empty;

    public string NameArabic { get; set; } = string.Empty;

    /// <summary><c>true</c> for audience tiers, <c>false</c> for partner and staff types.</summary>
    public bool IsForVisitor { get; set; } = true;

    /// <summary>Hex or CSS-variable colour of the physical badge and bag, shown at the gate.</summary>
    public string PageColor { get; set; } = string.Empty;

    public MobileAppRole MobileAppRole { get; set; } = MobileAppRole.None;

    /// <summary>The VIP-tier marker: who may self-reserve a VIP seat, sent to the app as <c>isVip</c>.
    /// It does not gate meeting requests; <c>UserProfile.AllowsSpeakerMeeting</c> does.</summary>
    public bool AllowsVipMeetingSlots { get; set; }

    /// <summary>Whether the mobile sign-up picker offers this type; Control Panel listings show every type.</summary>
    public bool IsAppRegisterable { get; set; } = true;

    /// <summary>Admin master switch AND-combined with the per-user <c>UserProfile.ShowInMeetLikeYou</c> opt-in.</summary>
    public bool ShowInPartnerDirectory { get; set; } = true;

    /// <summary>Small stable number the offline badge carries instead of the Guid id, so a gate can
    /// decide from the QR alone. Assigned once and never reused: printed badges stay in circulation.</summary>
    public short Code { get; set; }
}
