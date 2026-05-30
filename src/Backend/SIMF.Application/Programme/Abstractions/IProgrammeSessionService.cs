using SIMF.Contracts.Programme;

namespace SIMF.Application.Programme.Abstractions;

/// <summary>D-199 (gap doc G3, Mockup pages 16-17) — public, anonymous
/// reads over the programme <c>Session</c> surface: the agenda list and
/// the single-session detail. Read-only; the admin CRUD lives on
/// <see cref="IAdminSessionService"/>. Mirrors the
/// <c>IPublicDelegationService</c> public-read shape.</summary>
public interface IProgrammeSessionService
{
    /// <summary>Active sessions ordered by start time, optionally
    /// restricted to a single calendar day (UTC). Used by the agenda
    /// screen's Day 1/2/3 segmented control.</summary>
    Task<PublicSessions> ListAsync(
        DateOnly? day, CancellationToken cancellationToken = default);

    /// <summary>Full public detail for one active session, or null when
    /// the session does not exist or has been soft-deleted.</summary>
    Task<PublicSessionDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default);
}
