using SIMF.Domain.IdentityAccess;

namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>Persistence for <see cref="SecondFactorToken"/> tickets.</summary>
public interface ISecondFactorTokenRepository
{
    Task AddAsync(SecondFactorToken token, CancellationToken cancellationToken = default);

    /// <summary>Finds a ticket by its hash; null if there is no match.</summary>
    Task<SecondFactorToken?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically consumes the ticket in a single UPDATE, but only while it is
    /// still unconsumed. Returns <c>true</c> only for the caller that flips
    /// <see cref="SecondFactorToken.ConsumedAt"/> from null to
    /// <paramref name="now"/> — a concurrent second verify of the same ticket
    /// gets <c>false</c> and must reject, so one ticket mints exactly one
    /// session (closes the read-modify-write double-mint window).
    /// </summary>
    Task<bool> TryConsumeAsync(
        Guid tokenId, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically increments the wrong-code attempt counter in a single UPDATE,
    /// so concurrent wrong submissions cannot lose increments and hand back
    /// brute-force budget (a read-modify-write <c>AttemptCount++</c> could). The
    /// cap itself is still checked as a separate read in the caller, so a
    /// simultaneous burst can slip a few guesses past the gate — a pre-existing
    /// check-then-act window this does not widen.
    /// </summary>
    Task IncrementAttemptCountAsync(
        Guid tokenId, CancellationToken cancellationToken = default);

    Task UpdateAsync(SecondFactorToken token, CancellationToken cancellationToken = default);
}
