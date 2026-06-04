using SIMF.Contracts.Exhibition;

namespace SIMF.Application.Exhibition.Abstractions;

/// <summary>D-199 — public, anonymous read access to active booths
/// (Mockup page 22 + the 2D venue map). Mirrors IPublicDelegationService.</summary>
public interface IPublicBoothService
{
    /// <summary>All active booths, ordered by Code. Drives the public
    /// exhibition list and the 2D map.</summary>
    Task<IReadOnlyList<PublicBoothSummary>> ListAsync(
        CancellationToken cancellationToken = default);

    /// <summary>One active booth by id, or null when it is missing or
    /// soft-deleted.</summary>
    Task<PublicBoothDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default);
}
