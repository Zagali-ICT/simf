using SIMF.Contracts.UserProfile;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// User self-service profile management (renamed from
/// <c>IVisitorProfileService</c>). Every call carries the
/// actor's user id — the user can only see and edit their own row. The
/// wider admin-edits-anyone flow lives in the User Management module
/// and is out of scope here.
/// </summary>
public interface IUserProfileService
{
    /// <summary>Returns the actor's profile. When the user has not
    /// filled the form yet, returns an empty response carrying only the
    /// QR id (if minted) and the admin-assigned ProfileTypeId (if any).</summary>
    Task<UserProfileResponse> GetMineAsync(
        Guid actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates the actor's profile.</summary>
    Task<UserProfileResponse> UpsertMineAsync(
        Guid actorUserId,
        UpsertUserProfileRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Saves the supplied ID-image bytes (already magic-byte
    /// validated by the caller) encrypted-at-rest. Replaces a previous
    /// image for the same user.</summary>
    Task UploadIdImageAsync(
        Guid actorUserId,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>Reads the actor's ID-image and decrypts it. Returns null
    /// when no image is set.</summary>
    Task<UserIdDocumentImage?> ReadIdImageAsync(
        Guid actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Admin-side upload of an ID-image for a subject other
    /// than the actor. Used by the walk-in registration desk to capture
    /// the visitor's national ID / passport scan on behalf of them. The
    /// caller is responsible for the role/policy check at the endpoint;
    /// this method only enforces UserType match (Visitor / Other) so an
    /// Admin row can never accidentally receive an ID image.</summary>
    Task UploadIdImageForSubjectAsync(
        Guid actorUserId,
        Guid subjectUserId,
        SIMF.Common.Enums.UserType expectedKind,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>Admin-side read of a subject's ID-image. Same
    /// UserType-match guard as the upload variant. A9 (PII) — takes the acting
    /// admin's <paramref name="actorUserId"/> so every byte disclosure writes a
    /// <c>UserProfile.IdImageViewed</c> audit row (mirrors the upload's audit).</summary>
    Task<UserIdDocumentImage?> ReadIdImageForSubjectAsync(
        Guid actorUserId,
        Guid subjectUserId,
        SIMF.Common.Enums.UserType expectedKind,
        CancellationToken cancellationToken = default);

    /// <summary>Admin-side upload of the VVIP/VIP welcome photo
    /// (صورة واضحة) for a subject. Stored in the dedicated VIP-photo store and
    /// written to <c>UserProfile.VipPhotoRelativePath</c> — distinct from both
    /// the account avatar and the ID image. The caller enforces the role/policy
    /// check + the size/MIME/magic-byte gate at the endpoint; this method only
    /// enforces the UserType match (Visitor) so an Admin row can never receive
    /// a VIP photo.</summary>
    Task UploadVipPhotoForSubjectAsync(
        Guid actorUserId,
        Guid subjectUserId,
        SIMF.Common.Enums.UserType expectedKind,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>Admin-side read of a subject's VIP welcome photo.
    /// Same UserType-match guard as the upload variant; null when no photo is
    /// The actor is audited on every byte disclosure
    /// (PII trail), mirroring <see cref="ReadIdImageForSubjectAsync"/>.</summary>
    Task<VipPhotoImage?> ReadVipPhotoForSubjectAsync(
        Guid actorUserId,
        Guid subjectUserId,
        SIMF.Common.Enums.UserType expectedKind,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Focused read for the SignInService state-banner — returns
    /// the bilingual rejection text for the user (or <c>null</c> when no
    /// UserProfile row exists or no rejection is on record). The
    /// rejection text lives on the profile now; SignInService
    /// used to read it directly off the user row.
    /// </summary>
    Task<RejectionText?> GetRejectionTextAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Resolves the mobile-app role that the
    /// <see cref="IJwtTokenService.CreateAccessToken"/> claim
    /// <c>mobile_app_role</c> should carry for this user. Visitors are
    /// always <see cref="SIMF.Common.Enums.MobileAppRole.Visitor"/>; Others look up the
    /// assigned <c>ProfileType.MobileAppRole</c> (or
    /// <see cref="SIMF.Common.Enums.MobileAppRole.None"/> when no profile-type is assigned
    /// or it explicitly says None); Admins are
    /// <see cref="SIMF.Common.Enums.MobileAppRole.None"/> (admins do not use the mobile
    /// app).</summary>
    Task<SIMF.Common.Enums.MobileAppRole> ResolveMobileAppRoleAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Server-computed profile completeness (names + ≥1
    /// interest + the male face-photo rule). Surfaced on the login flow's
    /// <c>/app/users/me</c> so the app forces the add-profile stage after
    /// ANY login path without a separate probe.</summary>
    Task<bool> IsProfileCompleteAsync(
        Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>The decrypted ID-image bytes + the content type for the response.</summary>
public sealed record UserIdDocumentImage(byte[] Content, string ContentType);

/// <summary>The VVIP/VIP welcome-photo bytes + content type for
/// the stream response (not encrypted; a portrait shared with the موج teams).</summary>
public sealed record VipPhotoImage(byte[] Content, string ContentType);

/// <summary>Bilingual rejection text projection (EN + AR).</summary>
public sealed record RejectionText(string? English, string? Arabic);
