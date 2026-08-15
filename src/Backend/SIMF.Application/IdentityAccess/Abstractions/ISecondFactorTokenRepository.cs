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
    /// Atomically increments the wrong-code attempt counter in a single UPDATE
    /// and returns the new count, so concurrent wrong submissions cannot lose
    /// increments and hand back brute-force budget (a read-modify-write
    /// <c>AttemptCount++</c> could).
    ///
    /// <para>The cap is still read separately when the ticket is fetched, which
    /// on its own leaves a check-then-act window a simultaneous burst could slip
    /// through. Callers close it by deciding on the count returned here — taken
    /// after the guess was compared, unlike the fetched value — and burning the
    /// ticket once the budget is spent.</para>
    /// </summary>
    Task<int> IncrementAttemptCountAsync(
        Guid tokenId, CancellationToken cancellationToken = default);

    Task UpdateAsync(SecondFactorToken token, CancellationToken cancellationToken = default);
}
