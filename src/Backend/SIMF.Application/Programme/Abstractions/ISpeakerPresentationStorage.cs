namespace SIMF.Application.Programme.Abstractions;

/// <summary>P2.3 — D-228 (FR-407): binary storage for speaker presentation
/// files. Bytes live OUTSIDE the database row (decision D-90, same policy as
/// <c>IMediaImageStorage</c>) so the <c>SpeakerPresentations</c> table stays
/// lean. Implemented by the filesystem adapter in Infrastructure; the returned
/// storage key is persisted on the row and passed back to read/delete.</summary>
public interface ISpeakerPresentationStorage
{
    Task<string> SaveAsync(
        Guid presentationId, byte[] content, string originalFileName,
        CancellationToken cancellationToken = default);

    Task<(byte[] Content, string ContentType)?> GetAsync(
        string storedFileName, string contentType,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string storedFileName, CancellationToken cancellationToken = default);
}
