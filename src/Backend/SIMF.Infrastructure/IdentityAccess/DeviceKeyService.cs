// Tests: SIMF.Api.Tests/DeviceKeySignInTests.cs
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.IdentityAccess;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.IdentityAccess;

/// <summary>
/// D-172 (gap doc G10, PDF §2.5) — Face ID / Touch ID sign-in.
/// Owns the four steps: register a new key, list my keys, issue a
/// per-key challenge, verify a signed challenge + mint tokens. The
/// token-mint helper is structurally the same path
/// <see cref="SIMF.Application.IdentityAccess.SignInService"/>'s
/// private <c>IssueTokensAsync</c> takes — extracting a shared
/// <c>ITokenIssuer</c> abstraction would be the right follow-up;
/// the current path duplicates the ~15 lines and is documented as
/// such in <see cref="MintTokensAsync"/>.
/// </summary>
internal sealed class DeviceKeyService(
    SimfIdentityDbContext identityDbContext,
    IUserAccountRepository accounts,
    IRefreshTokenRepository refreshTokenRepository,
    IUserProfileService userProfiles,
    IJwtTokenService jwtTokenService,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<DeviceKeyService> logger) : IDeviceKeyService
{
    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    public async Task<DeviceKeyEntry> RegisterAsync(
        Guid callerUserId,
        RegisterDeviceKeyRequest request,
        CancellationToken cancellationToken = default)
    {
        var publicKey = (request.PublicKey ?? string.Empty).Trim();
        var algorithm = (request.Algorithm ?? string.Empty).Trim();
        var label = (request.Label ?? string.Empty).Trim();

        if (string.IsNullOrEmpty(publicKey) || publicKey.Length > 256)
        {
            throw new ApiException(
                ErrorCodes.DeviceKeyInvalid, 400,
                "Public key is missing or too large.",
                "المفتاح العام مفقود أو كبير جداً.");
        }
        if (algorithm != "ES256")
        {
            throw new ApiException(
                ErrorCodes.DeviceKeyAlgorithmUnsupported, 400,
                "Only ES256 (ECDSA P-256) is supported.",
                "يُدعم فقط ES256 (ECDSA P-256).");
        }
        if (label.Length is < 1 or > 64)
        {
            throw new ApiException(
                ErrorCodes.DeviceKeyInvalid, 400,
                "Device label must be between 1 and 64 characters.",
                "يجب أن يتراوح طول اسم الجهاز بين 1 و 64 حرفاً.");
        }

        // Eagerly validate the public key is a parseable SubjectPublicKeyInfo
        // for the chosen curve — failure here is a 400, not a 500 later.
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey),
                out _);
        }
        catch (Exception)
        {
            throw new ApiException(
                ErrorCodes.DeviceKeyInvalid, 400,
                "Public key could not be parsed as ECDSA P-256.",
                "تعذّر قراءة المفتاح العام كـ ECDSA P-256.");
        }

        var now = timeProvider.GetUtcNow();
        var deviceKey = new DeviceKey
        {
            Id = Guid.NewGuid(),
            UserId = callerUserId,
            PublicKey = publicKey,
            Algorithm = algorithm,
            Label = label,
            CreatedAt = now,
        };
        identityDbContext.DeviceKeys.Add(deviceKey);
        await identityDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.DeviceKeyRegistered,
            Outcome = AuditOutcome.Success,
            ActorUserId = callerUserId,
            SubjectUserId = callerUserId,
            Detail = $"deviceKeyId={deviceKey.Id}; label={label}",
        }, cancellationToken);

        logger.LogInformation(
            "Device key {DeviceKeyId} registered for user {UserId}",
            deviceKey.Id, callerUserId);

        return ToEntry(deviceKey);
    }

    public async Task<IReadOnlyList<DeviceKeyEntry>> ListMineAsync(
        Guid callerUserId,
        CancellationToken cancellationToken = default)
    {
        var rows = await identityDbContext.DeviceKeys
            .AsNoTracking()
            .Where(k => k.UserId == callerUserId)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(ToEntry).ToList();
    }

    public async Task<DeviceKeyChallenge> IssueChallengeAsync(
        Guid deviceKeyId,
        CancellationToken cancellationToken = default)
    {
        var deviceKey = await identityDbContext.DeviceKeys
            .SingleOrDefaultAsync(k => k.Id == deviceKeyId, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.DeviceKeyNotFound, 404,
                "Device key not found.",
                "لم يتم العثور على مفتاح الجهاز.");

        if (deviceKey.RevokedAt is not null)
        {
            throw new ApiException(
                ErrorCodes.DeviceKeyRevoked, 401,
                "Device key is revoked.",
                "تم إلغاء مفتاح الجهاز.");
        }

        // 32-byte cryptographic random — ample for a single-use nonce.
        var bytes = RandomNumberGenerator.GetBytes(32);
        var challenge = Convert.ToBase64String(bytes);
        var now = timeProvider.GetUtcNow();
        deviceKey.CurrentChallenge = challenge;
        deviceKey.ChallengeExpiresAt = now.Add(ChallengeLifetime);
        await identityDbContext.SaveChangesAsync(cancellationToken);

        return new DeviceKeyChallenge(
            challenge,
            (int)ChallengeLifetime.TotalSeconds);
    }

    public async Task<AuthTokens?> SignInWithDeviceKeyAsync(
        SignInWithDeviceKeyRequest request,
        CancellationToken cancellationToken = default)
    {
        var deviceKey = await identityDbContext.DeviceKeys
            .SingleOrDefaultAsync(k => k.Id == request.DeviceKeyId, cancellationToken);
        if (deviceKey is null
            || deviceKey.RevokedAt is not null
            || deviceKey.CurrentChallenge is null
            || deviceKey.ChallengeExpiresAt is null
            || deviceKey.ChallengeExpiresAt <= timeProvider.GetUtcNow())
        {
            await AuditFailureAsync(request.DeviceKeyId,
                ErrorCodes.DeviceKeyChallengeInvalid, "expired_or_missing",
                cancellationToken);
            return null;
        }

        // Bind the supplied challenge to the stored one — the client
        // must have signed exactly the challenge the server just issued.
        if (!string.Equals(request.Challenge ?? string.Empty,
                deviceKey.CurrentChallenge, StringComparison.Ordinal))
        {
            await AuditFailureAsync(request.DeviceKeyId,
                ErrorCodes.DeviceKeyChallengeInvalid, "mismatch",
                cancellationToken);
            return null;
        }

        if (!VerifySignature(
                deviceKey.PublicKey,
                deviceKey.CurrentChallenge,
                request.Signature ?? string.Empty))
        {
            await AuditFailureAsync(request.DeviceKeyId,
                ErrorCodes.DeviceKeySignatureInvalid, "bad_signature",
                cancellationToken);
            return null;
        }

        // Consume the challenge so the same signature cannot be replayed.
        deviceKey.CurrentChallenge = null;
        deviceKey.ChallengeExpiresAt = null;
        deviceKey.LastUsedAt = timeProvider.GetUtcNow();
        await identityDbContext.SaveChangesAsync(cancellationToken);

        var user = await accounts.FindByIdAsync(deviceKey.UserId, cancellationToken);
        if (user is null || user.AccountState == AccountState.Disabled)
        {
            await AuditFailureAsync(request.DeviceKeyId,
                ErrorCodes.DeviceKeyOwnerUnavailable, "user_missing_or_disabled",
                cancellationToken);
            return null;
        }

        return await MintTokensAsync(user, cancellationToken);
    }

    public async Task RevokeAsync(
        Guid actorUserId,
        Guid deviceKeyId,
        bool actorIsAdministrator,
        CancellationToken cancellationToken = default)
    {
        var deviceKey = await identityDbContext.DeviceKeys
            .SingleOrDefaultAsync(k => k.Id == deviceKeyId, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.DeviceKeyNotFound, 404,
                "Device key not found.",
                "لم يتم العثور على مفتاح الجهاز.");

        if (!actorIsAdministrator && deviceKey.UserId != actorUserId)
        {
            throw new ApiException(
                ErrorCodes.DeviceKeyNotFound, 404,
                "Device key not found.",
                "لم يتم العثور على مفتاح الجهاز.");
        }

        if (deviceKey.RevokedAt is not null)
        {
            return; // idempotent
        }

        deviceKey.RevokedAt = timeProvider.GetUtcNow();
        deviceKey.CurrentChallenge = null;
        deviceKey.ChallengeExpiresAt = null;
        await identityDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.DeviceKeyRevoked,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            SubjectUserId = deviceKey.UserId,
            Detail = $"deviceKeyId={deviceKey.Id}; admin={actorIsAdministrator}",
        }, cancellationToken);
    }

    // -- helpers --------------------------------------------------------------

    /// <summary>Verify an ES256 signature over the challenge bytes.
    /// The signature is the IEEE-P1363 raw (r || s, 64 bytes) format
    /// as base64. Matches JWS ES256 and what every mobile crypto lib
    /// (CryptoKit on iOS, Tink on Android) returns by default.</summary>
    private static bool VerifySignature(
        string publicKeyBase64, string challengeBase64, string signatureBase64)
    {
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(
                Convert.FromBase64String(publicKeyBase64), out _);

            var challengeBytes = Convert.FromBase64String(challengeBase64);
            var signatureBytes = Convert.FromBase64String(signatureBase64);

            return ecdsa.VerifyData(
                challengeBytes, signatureBytes, HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>The token-mint path. Structurally the same shape
    /// <see cref="SignInService"/>.IssueTokensAsync uses; the follow-up
    /// is to extract a shared <c>ITokenIssuer</c>. Documented in the
    /// class XML doc above.</summary>
    private async Task<AuthTokens> MintTokensAsync(
        SimfUser user, CancellationToken cancellationToken)
    {
        var roles = await accounts.GetRolesAsync(user);
        var mobileAppRole = await userProfiles
            .ResolveMobileAppRoleAsync(user.Id, cancellationToken);
        var accessToken = jwtTokenService.CreateAccessToken(user, roles, mobileAppRole);

        var refreshValue = OpaqueToken.Generate();
        var now = timeProvider.GetUtcNow();
        await refreshTokenRepository.AddAsync(
            new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = OpaqueToken.Hash(refreshValue),
                CreatedAt = now,
                ExpiresAt = now.Add(RefreshTokenLifetime),
            },
            cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.RefreshTokenIssued,
            Outcome = AuditOutcome.Success,
            SubjectUserId = user.Id,
            SubjectEmail = user.Email,
        }, cancellationToken);
        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SignInWithDeviceKey,
            Outcome = AuditOutcome.Success,
            ActorUserId = user.Id,
            SubjectUserId = user.Id,
            SubjectEmail = user.Email,
        }, cancellationToken);
        logger.LogInformation(
            "Device-key sign-in completed for {Email}", user.Email);

        return new AuthTokens(
            accessToken.Value,
            refreshValue,
            "Bearer",
            accessToken.ExpiresInSeconds,
            new AuthUser(user.Id, user.Email!, user.DisplayName));
    }

    private async Task AuditFailureAsync(
        Guid deviceKeyId, string errorCode, string detail,
        CancellationToken cancellationToken)
    {
        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SignInWithDeviceKeyFailed,
            Outcome = AuditOutcome.Failure,
            ErrorCode = errorCode,
            Detail = $"deviceKeyId={deviceKeyId}; reason={detail}",
        }, cancellationToken);
    }

    private static DeviceKeyEntry ToEntry(DeviceKey deviceKey) =>
        new(deviceKey.Id, deviceKey.UserId, deviceKey.Algorithm,
            deviceKey.Label, deviceKey.CreatedAt, deviceKey.LastUsedAt,
            deviceKey.RevokedAt);
}
