namespace SIMF.Application.Abstractions;

/// <summary>D-199 — binary storage for gallery media images. Implemented by
/// the filesystem adapter in Infrastructure; bytes live OUTSIDE the database
/// row (decision D-90, the same policy as <see cref="IAvatarStorage"/>) so
/// the <c>MediaItems</c> table stays lean. <paramref name="slot"/>
/// distinguishes the primary image from the thumbnail/poster for one item.</summary>
public interface IMediaImageStorage
{
    Task<string> SaveAsync(
        Guid mediaItemId, MediaImageSlot slot, byte[] content, string contentType,
        CancellationToken cancellationToken = default);

    Task<(byte[] Content, string ContentType)?> GetAsync(
        string relativePath, CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string relativePath, CancellationToken cancellationToken = default);
}

/// <summary>D-199 — which image of a media item the bytes belong to.</summary>
public enum MediaImageSlot
{
    Image = 0,
    Thumbnail = 1,
}
