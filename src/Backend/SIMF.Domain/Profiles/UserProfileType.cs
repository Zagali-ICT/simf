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

    /// <summary>Marks this type as a VIP audience tier. It decides who may
    /// self-reserve a VIP-tier seat
    /// (<c>SeatReservationService.IsVipVisitorAsync</c>) and it is what the app
    /// receives as <c>isVip</c>. The seeder sets it on the VVIP and VIP rows;
    /// an admin sets it on any other audience tier from the Control Panel's
    /// Visitors profile-types form. A partner type is never a VIP tier, so that
    /// form does not offer the toggle.
    ///
    /// <para>It is a column at all because the test used to be "the profile
    /// type's Name contains 'VIP'", which would wrongly match any future type
    /// whose name merely embedded those letters.</para>
    ///
    /// <para>It does <b>not</b> gate meeting requests. Meeting eligibility is
    /// assigned per user on <c>UserProfile.AllowsSpeakerMeeting</c> /
    /// <c>AllowsDelegationMeeting</c>, so two people on the same tier can differ.
    /// This column was once named for that job and was renamed when those flags
    /// took it over: a meeting-shaped name reads at the call site as an answer to
    /// "may this person request a meeting?", which is exactly what it stopped
    /// answering. The only question it answers is "is this a VIP tier?".</para></summary>
    public bool IsVipTier { get; set; }

    /// <summary>Whether the mobile sign-up picker offers this type; Control Panel listings show every type.</summary>
    public bool IsAppRegisterable { get; set; } = true;

    /// <summary>Admin master switch AND-combined with the per-user <c>UserProfile.ShowInMeetLikeYou</c> opt-in.</summary>
    public bool ShowInPartnerDirectory { get; set; } = true;

    /// <summary>Small stable number the offline badge carries instead of the Guid id, so a gate can
    /// decide from the QR alone. Assigned once and never reused: printed badges stay in circulation.</summary>
    public short Code { get; set; }
}
