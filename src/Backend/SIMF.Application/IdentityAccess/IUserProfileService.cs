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
}

/// <summary>The decrypted ID-image bytes + the content type for the response.</summary>
public sealed record UserIdDocumentImage(byte[] Content, string ContentType);
