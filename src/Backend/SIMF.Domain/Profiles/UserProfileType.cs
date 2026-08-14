using SIMF.Common.Enums;
using SIMF.Domain.Common;

namespace SIMF.Domain.Profiles;

/// <summary>
/// A subtype an admin assigns to a <see cref="IdentityAccess.SimfUser"/>: VVIP, Gold,
/// Exhibitor, Staff and so on. The rows are curated from the Control Panel at
/// runtime, so a new subtype is a row insert rather than a code change.
///
/// <para>Every profile type is Visitor-scope. There is no user-type column on
/// the row, and the create endpoint refuses any other scope; the
/// audience-versus-partner split lives on <see cref="IsForVisitor"/>.</para>
///
/// <para><b>A profile type grants no permissions.</b> It is display and
/// business-rule metadata: which badge colour, which approval queue, which
/// authority in the app. Permission checks key off <see cref="UserType"/> and
/// the user's roles, never off this row.</para>
/// </summary>
public sealed class UserProfileType : BaseAuditEntity
{
    public string Name { get; set; } = string.Empty;

    public string NameArabic { get; set; } = string.Empty;

    /// <summary>The audience-versus-partner split. <c>true</c> for audience
    /// tiers (VVIP, VIP, Gold, Normal), <c>false</c> for partner and staff
    /// types (Sponsor, Exhibitor, Media, Staff). The Control Panel's two
    /// approval queues, Visitors and Others, are one query with this flag
    /// flipped, kept as separate pages so an approving admin cannot confuse the
    /// two populations. Defaults to <c>true</c>, so a brand-new type is assumed
    /// audience-side until an admin says otherwise.</summary>
    public bool IsForVisitor { get; set; } = true;

    /// <summary>
    /// A hex string such as <c>#FFD700</c>, or a CSS variable name. This is the
    /// physical bag and badge colour for the event, so the gate app shows it
    /// beside a scanned attendee to let an operator match it by eye.
    /// </summary>
    public string PageColor { get; set; } = string.Empty;

    /// <summary>The authority a holder of this type carries into the Flutter
    /// app. <see cref="MobileAppRole.None"/> for audience tiers and for partner
    /// types with no operational duties, which the app then treats as plain
    /// visitors. Admin-curated, so giving a new type staff or moderator powers
    /// is a checkbox rather than a code change.</summary>
    public MobileAppRole MobileAppRole { get; set; } = MobileAppRole.None;

    /// <summary>Marks this type as a VIP audience tier. It decides who may
    /// self-reserve a VIP-tier seat
    /// (<c>SeatReservationService.IsVipVisitorAsync</c>) and it is what the app
    /// receives as <c>isVip</c>. The seeder sets it on the VVIP and VIP rows, and
    /// nothing else writes it — there is no admin API or Control Panel path, so a
    /// type created from the Control Panel is never VIP.
    ///
    /// <para>It is a column at all because the test used to be "the profile
    /// type's Name contains 'VIP'", which would wrongly match any future type
    /// whose name merely embedded those letters.</para>
    ///
    /// <para>It does <b>not</b> gate meeting requests. It was called
    /// <c>AllowsVipMeetingSlots</c> until D-760 moved speaker-meeting eligibility
    /// to the per-user <c>UserProfile.AllowsSpeakerMeeting</c> flag, leaving a
    /// name that described a job this column no longer had.</para></summary>
    public bool IsVipTier { get; set; }

    /// <summary>Whether a self-registering user is offered this type in the
    /// mobile sign-up picker. <c>false</c> for operational types an admin
    /// assigns rather than a customer self-selecting, such as Staff and
    /// Moderator. Defaults to <c>true</c>, so a brand-new type is offered until
    /// an admin hides it. It affects that one picker: Control Panel listings
    /// still show every type, and it confers nothing.</summary>
    public bool IsAppRegisterable { get; set; } = true;

    /// <summary>The admin master switch for the "Meet People" surfaces, being
    /// the partner directory and the app's recommender. It AND-combines with
    /// the per-user <c>UserProfile.ShowInMeetLikeYou</c> opt-in, so clearing it
    /// removes every account of this type from those surfaces however the
    /// individuals set their own preference. Defaults to <c>true</c>. Only
    /// partner types are curated this way, and the Control Panel shows the
    /// checkbox on the Others form alone.</summary>
    public bool ShowInPartnerDirectory { get; set; } = true;

    /// <summary>
    /// A small stable number identifying this profile type inside the offline
    /// event badge. <see cref="BaseAuditEntity.Id"/> is a Guid and will not fit
    /// in a QR that has to stay readable when a badge is creased, so the badge
    /// carries this code instead.
    ///
    /// <para>Carrying the type in the badge is what lets a gate decide OFFLINE:
    /// it decrypts the badge, reads the code, and checks it against that gate's
    /// allowed-profile-type list with no lookup and no network.</para>
    ///
    /// <para>Allocated on create, never typed by an admin, because it is a wire
    /// detail of the QR rather than something to pick and keep unique. Assigned
    /// once and never reused, because badges already printed under a code stay
    /// in circulation. 0 means "not assigned", which no badge can carry.</para>
    /// </summary>
    public short Code { get; set; }
}
