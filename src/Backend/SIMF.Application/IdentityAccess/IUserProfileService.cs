using SIMF.Contracts.UserProfile;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// User self-service profile management (decisions D-046 b, P8 — D-049;
/// renamed from <c>IVisitorProfileService</c>). Every call carries the
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

    /// <summary>D-129: admin-side upload of an ID-image for a subject other
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

    /// <summary>D-129: admin-side read of a subject's ID-image. Same
    /// UserType-match guard as the upload variant.</summary>
    Task<UserIdDocumentImage?> ReadIdImageForSubjectAsync(
        Guid subjectUserId,
        SIMF.Common.Enums.UserType expectedKind,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// D-106: focused read for the SignInService state-banner — returns
    /// the bilingual rejection text for the user (or <c>null</c> when no
    /// UserProfile row exists or no rejection is on record). The
    /// rejection text lives on the profile post-D-106; SignInService
    /// used to read it directly off the user row.
    /// </summary>
    Task<RejectionText?> GetRejectionTextAsync(
        Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>The decrypted ID-image bytes + the content type for the response.</summary>
public sealed record UserIdDocumentImage(byte[] Content, string ContentType);

/// <summary>D-106: bilingual rejection text projection (EN + AR).</summary>
public sealed record RejectionText(string? English, string? Arabic);
