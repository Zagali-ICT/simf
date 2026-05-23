using SIMF.Contracts.VisitorProfile;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// Visitor self-service profile management (decision D-046 b, myComment #18).
/// Every call carries the actor's user id — the visitor can only see and
/// edit their own row. The wider admin-edits-anyone flow lives in the
/// User Management module and is out of scope here.
/// </summary>
public interface IVisitorProfileService
{
    /// <summary>Returns the actor's profile. When the visitor has not
    /// filled the form yet, returns an empty response carrying only the
    /// QR id (if minted).</summary>
    Task<VisitorProfileResponse> GetMineAsync(
        Guid actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates the actor's profile.</summary>
    Task<VisitorProfileResponse> UpsertMineAsync(
        Guid actorUserId,
        UpsertVisitorProfileRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Saves the supplied ID-image bytes (already magic-byte
    /// validated by the caller) encrypted-at-rest. Replaces a previous
    /// image for the same visitor.</summary>
    Task UploadIdImageAsync(
        Guid actorUserId,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>Reads the actor's ID-image and decrypts it. Returns null
    /// when no image is set.</summary>
    Task<VisitorIdImage?> ReadIdImageAsync(
        Guid actorUserId, CancellationToken cancellationToken = default);
}

/// <summary>The decrypted ID-image bytes + the content type for the response.</summary>
public sealed record VisitorIdImage(byte[] Content, string ContentType);
