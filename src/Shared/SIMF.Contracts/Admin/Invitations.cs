using SIMF.Common.Enums;

namespace SIMF.Contracts.Admin;

/// <summary>D-168 (gap doc G5, PDF §2.7.3) — one row in the admin
/// Invitations grid. The recipient's display name + profile type are
/// projected so the grid does not need a second fetch.</summary>
public sealed record AdminInvitationSummary(
    Guid Id,
    Guid SentByUserId,
    string SentByDisplayName,
    Guid SentToUserProfileId,
    string RecipientEnglishName,
    string RecipientArabicName,
    string? RecipientProfileTypeName,
    string? RecipientEmail,
    InvitationState State,
    string? Notes,
    DateTime CreatedAt,
    DateTime? RespondedAt,
    bool IsActive);

/// <summary>D-168 — full invitation detail. Same projection plus the
/// recipient's job title + nationality so the Details / Edit modal can
/// render the recipient card without a second fetch.</summary>
public sealed record AdminInvitationDetail(
    Guid Id,
    Guid SentByUserId,
    string SentByDisplayName,
    Guid SentToUserProfileId,
    string RecipientEnglishName,
    string RecipientArabicName,
    string? RecipientProfileTypeName,
    string? RecipientEmail,
    string? RecipientJobTitle,
    InvitationState State,
    string? Notes,
    DateTime CreatedAt,
    DateTime? RespondedAt,
    DateTime? UpdatedAt,
    bool IsActive);

/// <summary>D-168 — create-invitation request. State defaults to
/// <see cref="InvitationState.Pending"/> on the server; the PR rep
/// only specifies who + an optional note.</summary>
public sealed class AdminCreateInvitationRequest
{
    public Guid SentToUserProfileId { get; set; }
    public string? Notes { get; set; }
}

/// <summary>D-168 — update-invitation request. The PR rep may move the
/// state forward (Pending → Confirmed / Declined) and edit the notes.
/// Going back to <see cref="InvitationState.Pending"/> from a settled
/// state is rejected by the service (the recipient already responded).
/// Open for inheritance so the route-binding endpoint can add an
/// <c>Id</c> field without a separate DTO indirection.</summary>
public class AdminUpdateInvitationRequest
{
    public InvitationState State { get; set; }
    public string? Notes { get; set; }
}

/// <summary>D-168 — one row in the admin VIP list. ProfileType.Name is
/// the discriminator (VVIP / VIP / Gold).</summary>
public sealed record AdminVipSummary(
    Guid UserProfileId,
    Guid UserId,
    string EnglishName,
    string ArabicName,
    string? JobTitle,
    string? JobTitleArabic,
    string ProfileTypeName,
    string ProfileTypeNameArabic,
    string? Email);

/// <summary>D-168 — bulk-notify-VIPs request. Body is the free-text
/// message; the dispatcher fans out one in-app row + one queued email
/// per recipient. <see cref="UserProfileIds"/> is the explicit selection
/// from the VIP grid — the server validates each id is on the VIP list
/// before dispatch.</summary>
public sealed class AdminNotifyVipsRequest
{
    public IList<Guid> UserProfileIds { get; set; } = new List<Guid>();
    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string BodyArabic { get; set; } = string.Empty;
}

/// <summary>D-168 — result of an
/// <see cref="AdminNotifyVipsRequest"/> dispatch. Per-recipient list of
/// the ids that successfully landed an in-app row + an enqueued email
/// (no-email entries indicate the recipient had no email on file).</summary>
public sealed record AdminNotifyVipsResult(
    int Dispatched,
    int EmailsEnqueued,
    IReadOnlyList<Guid> SkippedProfileIds);
