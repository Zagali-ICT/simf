namespace SIMF.Application.Abstractions;

/// <summary>D-357 — binary storage for unified media assets, mirroring
/// <see cref="IMediaImageStorage"/> / <see cref="IAvatarStorage"/>. Bytes live
/// OUTSIDE the database row (D-90). One file per asset id; re-upload replaces in
/// place. <see cref="SaveAsync"/> returns the relative file name the caller
/// persists on the <c>Asset</c> row.</summary>
public interface IImageAssetStorage
{
    Task<string> SaveAsync(
        Guid assetId, byte[] content, string contentType,
        CancellationToken cancellationToken = default);

    Task<(byte[] Content, string ContentType)?> GetAsync(
        string relativePath, CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string relativePath, CancellationToken cancellationToken = default);
}
