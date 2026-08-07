namespace SIMF.Application.Configuration.Abstractions;

/// <summary>The CP-uploaded hero background video for the singleton
/// Organization Profile. Stores the video in the centralized file store
/// (<c>FileService.OrganizationHeroVideo</c>: public, plaintext, seekable), points
/// the profile's <c>BackgroundVideoUrl</c> at the served <c>.mp4</c> route, and
/// resolves the active file for the public range-stream endpoint. Kept separate
/// from the full-document profile upsert (<see cref="IOrganizationProfileAdminService"/>)
/// so the media concern stays cohesive.</summary>
public interface IOrganizationHeroVideoService
{
    /// <summary>Stream an uploaded hero video to the store, retire any prior one, and
    /// set the profile's <c>BackgroundVideoUrl</c> to <paramref name="servedUrl"/>
    /// (the absolute <c>.mp4</c> route the app/website hero play). Invalidates the
    /// public read cache and audits the actor.</summary>
    Task SetAsync(
        Guid actorUserId, Stream content, string fileName, string contentType,
        string extension, string servedUrl, CancellationToken cancellationToken = default);

    /// <summary>Remove the active uploaded hero video and, when
    /// <c>BackgroundVideoUrl</c> still points at <paramref name="servedUrl"/>, clear
    /// it — never clobber a separately-pasted external / YouTube link. Invalidates
    /// the read cache and audits the actor.</summary>
    Task RemoveAsync(
        Guid actorUserId, string servedUrl, CancellationToken cancellationToken = default);

    /// <summary>The active hero-video file for the public range-stream endpoint, or
    /// null when none is uploaded.</summary>
    Task<HeroVideoPointer?> GetActivePointerAsync(CancellationToken cancellationToken = default);
}

/// <summary>The stored-file pointer the public hero-video stream endpoint needs to
/// open + range-serve the bytes.</summary>
public sealed record HeroVideoPointer(Guid FileId, string ContentType, string FileName);
