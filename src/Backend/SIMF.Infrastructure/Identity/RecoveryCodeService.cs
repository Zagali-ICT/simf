// Tests: SIMF.Api.Tests/RecoveryCodesTests.cs
using Microsoft.Extensions.Logging;
using SIMF.Application.IdentityAccess;
using SIMF.Application.IdentityAccess.Abstractions;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// Generates, persists, verifies and consumes the single-use recovery codes
/// (decision D-040). The plaintext codes leave the service only at issuance.
/// </summary>
internal sealed class RecoveryCodeService(
    ITotpRecoveryCodeRepository repository,
    TimeProvider timeProvider,
    ILogger<RecoveryCodeService> logger) : IRecoveryCodeService
{
    private const int CodesPerBatch = 10;

    public async Task<IReadOnlyList<string>> GenerateAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // A fresh batch wipes the previous one — old codes never coexist with
        // new ones, so a user who regenerates can never accidentally use an
        // already-shared (and possibly photographed) code.
        await repository.RevokeAllForUserAsync(userId, cancellationToken);

        var codes = new string[CodesPerBatch];
        var hashes = new string[CodesPerBatch];
        for (var i = 0; i < CodesPerBatch; i++)
        {
            codes[i] = RecoveryCode.Generate();
            hashes[i] = RecoveryCode.Hash(RecoveryCode.Normalise(codes[i]));
        }

        await repository.AddBatchAsync(
            userId, hashes, timeProvider.GetUtcNow(), cancellationToken);
        logger.LogInformation("Recovery codes generated for {UserId}", userId);
        return codes;
    }

    public Task<int> CountActiveAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        repository.CountActiveAsync(userId, cancellationToken);

    public async Task<bool> VerifyAndConsumeAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var normalised = RecoveryCode.Normalise(code);
        if (normalised.Length == 0)
        {
            return false;
        }

        var hash = RecoveryCode.Hash(normalised);
        var stored = await repository.FindActiveAsync(userId, hash, cancellationToken);
        if (stored is null)
        {
            return false;
        }

        await repository.ConsumeAsync(stored, timeProvider.GetUtcNow(), cancellationToken);
        logger.LogInformation("Recovery code consumed for {UserId}", userId);
        return true;
    }

    public Task RevokeAllAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        repository.RevokeAllForUserAsync(userId, cancellationToken);
}
