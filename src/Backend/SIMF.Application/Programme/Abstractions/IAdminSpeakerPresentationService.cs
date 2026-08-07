using SIMF.Contracts.Admin;

namespace SIMF.Application.Programme.Abstractions;

/// <summary>Admin management of speaker presentation files. Each file links a
/// speaker to a session and is stored out-of-row in the unified
/// <c>StoredFile</c> store.</summary>
public interface IAdminSpeakerPresentationService
{
    /// <summary>All active presentations for one speaker, newest first.</summary>
    Task<IReadOnlyList<AdminSpeakerPresentationRow>> ListForSpeakerAsync(
        Guid speakerId, CancellationToken cancellationToken = default);

    /// <summary>Uploads a presentation file for (speaker, session): validates
    /// both exist, stores the bytes, and inserts the metadata row.</summary>
    Task<AdminSpeakerPresentationRow> UploadAsync(
        Guid actorUserId, Guid speakerId, Guid sessionId,
        byte[] content, string fileName, string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>Fetches the stored file bytes for download; null if missing.</summary>
    Task<(byte[] Content, string ContentType, string FileName)?> GetFileAsync(
        Guid presentationId, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes the row and removes the stored file.</summary>
    Task DeleteAsync(
        Guid actorUserId, Guid presentationId,
        CancellationToken cancellationToken = default);
}
