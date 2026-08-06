using SIMF.Contracts.Programme;

namespace SIMF.Application.Programme.Abstractions;

/// <summary>The public, read-only view of the speaker-presentation files for
/// the app's "Session presentations" ("عروض الجلسات") screen. Lists every
/// active presentation (with its session + speaker) so the app can group by day
/// and offer a download; the file bytes are served out-of-row from the same
/// unified <c>StoredFile</c> store the admin side uses. Approved-account only
/// (attendee materials).</summary>
public interface IPublicSpeakerPresentationService
{
    /// <summary>All active presentations, time-ordered by session start.</summary>
    Task<PublicPresentations> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>The stored file bytes for one presentation; null if missing /
    /// soft-deleted.</summary>
    Task<(byte[] Content, string ContentType, string FileName)?> GetFileAsync(
        Guid presentationId, CancellationToken cancellationToken = default);

    /// <summary>The stored file bytes for one presentation validated to belong to
    /// the given session; null if missing / soft-deleted / not in that session.
    /// Backs the public (anonymous) website Session-detail download route — the
    /// session scope is the authorisation check in place of a signed-in account.</summary>
    Task<(byte[] Content, string ContentType, string FileName)?> GetFileAsync(
        Guid sessionId, Guid presentationId, CancellationToken cancellationToken = default);
}
