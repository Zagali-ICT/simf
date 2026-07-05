using SIMF.Contracts.Programme;

namespace SIMF.Application.Programme.Abstractions;

/// <summary>Wave 2 (Figma 1388:7621 "عروض الجلسات") — the public, read-only view
/// of the speaker-presentation files for the app's "Session presentations"
/// screen. Lists every active presentation (with its session + speaker) so the
/// app can group by day and offer a download; the file bytes are served
/// out-of-row from the same unified <c>StoredFile</c> store the admin side uses
/// (D-568 Wave C S6). Approved-account only (attendee materials).</summary>
public interface IPublicSpeakerPresentationService
{
    /// <summary>All active presentations, time-ordered by session start.</summary>
    Task<PublicPresentations> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>The stored file bytes for one presentation; null if missing /
    /// soft-deleted.</summary>
    Task<(byte[] Content, string ContentType, string FileName)?> GetFileAsync(
        Guid presentationId, CancellationToken cancellationToken = default);
}
