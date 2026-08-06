using SIMF.Contracts.Programme;

namespace SIMF.Application.Programme.Abstractions;

/// <summary>Public, anonymous read access to
/// active speakers: the speakers list and the single-speaker profile.
/// Read-only sibling of <see cref="IAdminSpeakerService"/>; only active
/// speakers are returned. Mirrors <c>IPublicBoothService</c> /
/// <c>IProgrammeSessionService</c>.</summary>
public interface IPublicSpeakerService
{
    /// <summary>All active speakers ordered by <c>DisplayOrder</c> (then
    /// name). Drives the public speakers list.</summary>
    Task<PublicSpeakers> ListAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Full public profile for one active speaker — including the
    /// speaker's active sessions — or null when the speaker does not exist
    /// or has been soft-deleted.</summary>
    Task<PublicSpeakerDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default);
}
