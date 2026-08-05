// Tests: SIMF.Api.Tests/DeviceKeySignInTests.cs,
//        SIMF.Api.Tests/TokenIssuerParityTests.cs (itokenissuer-extraction — the
//        device-key session carries the same claim set and D-443 cap as the
//        password one)
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIMF.Application.Auditing;
using SIMF.Application.Email;
using SIMF.Application.IdentityAccess;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Options;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.IdentityAccess;

/// <summary>
/// D-172 (gap doc G10, PDF §2.5) — Face ID / Touch ID sign-in.
/// Owns the four steps: register a new key, list my keys, issue a
/// per-key challenge, verify a signed challenge + mint tokens.
///
/// <para>itokenissuer-extraction (2026-07-30) — the token mint is no longer a
/// local copy of <see cref="SIMF.Application.IdentityAccess.SignInService"/>'s:
/// both go through <see cref="ITokenIssuer"/>, so the claim set and the D-443
/// absolute session cap cannot drift between the two ways into the system.</para>
/// </summary>
internal sealed class DeviceKeyService(
    SimfIdentityDbContext identityDbContext,
    IUserAccountRepository accounts,
    ITokenIssuer tokenIssuer,
    IAccountCodeRepository accountCodes,
    IEmailQueue emailQueue,
    IEmailTemplateResolver emailTemplates,
    IOptions<DeviceKeyOptions> deviceKeyOptions,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<DeviceKeyService> logger) : IDeviceKeyService
{
    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(5);

    // #7a — emailed-OTP step-up, mirroring the sign-in OTP budget.
    private static readonly TimeSpan StepUpLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan StepUpRequestWindow = TimeSpan.FromHours(1);
    private const int MaxStepUpRequestsPerWindow = 5;
    private const int MaxStepUpAttempts = 5;

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

        var now = timeProvider.SimfNow();

        // #7a — emailed-OTP step-up: confirm the user actually intends to enable
        // biometric sign-in before binding a credential, so a borrowed-but-
        // unlocked phone can't silently enrol without also holding the account's
        // email. Validate (throws on a missing / wrong / expired code) but do NOT
        // consume yet — the code is single-used only AFTER the key is persisted
        // (below), so a failed key-save never burns a still-valid code and leaves
        // the user a dead code + a misleading "incorrect" on retry. Returns null
        // when the gate is configured off (registration crypto tests).
        var stepUpCode = await ValidateEnrolStepUpAsync(
            callerUserId, request.StepUpCode, now, cancellationToken);

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

        // Single-use: consume the step-up code now that the key is committed.
        if (stepUpCode is not null)
        {
            await accountCodes.TryConsumeAsync(stepUpCode.Id, now, cancellationToken);
        }

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

    public async Task<SendBiometricStepUpResponse> IssueEnrolStepUpAsync(
        Guid callerUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await accounts.FindByIdAsync(callerUserId, cancellationToken);
        if (user is null || string.IsNullOrEmpty(user.Email)
            || user.AccountState == AccountState.Disabled)
        {
            throw new ApiException(
                ErrorCodes.DeviceKeyOwnerUnavailable, 401,
                "Account unavailable.",
                "الحساب غير متاح.");
        }

        var now = timeProvider.SimfNow();

        // Cap how many step-up codes one account may request per window, so a
        // signed-in session can't be used to spam the address with emails.
        var recent = await accountCodes.CountCreatedSinceAsync(
            callerUserId, AccountCodePurpose.BiometricEnrolStepUp,
            now - StepUpRequestWindow, cancellationToken);
        if (recent >= MaxStepUpRequestsPerWindow)
        {
            await AuditStepUpRejectedAsync(callerUserId,
                ErrorCodes.RateLimitExceeded, "rate_limited", cancellationToken);
            throw new ApiException(
                ErrorCodes.RateLimitExceeded, 429,
                "Too many verification codes have been requested. Try again later.",
                "تم طلب رموز تحقق كثيرة. حاول مرة أخرى لاحقًا.");
        }

        // Only the newest code stays valid — consume any prior unconsumed one.
        var previous = await accountCodes.GetLatestUnconsumedAsync(
            callerUserId, AccountCodePurpose.BiometricEnrolStepUp, cancellationToken);
        if (previous is not null)
        {
            await accountCodes.TryConsumeAsync(previous.Id, now, cancellationToken);
        }

        // Store only the keyed hash; the plaintext is emailed and never persisted.
        var plaintext = VerificationCodeGenerator.Generate();
        await accountCodes.AddAsync(new AccountCode
        {
            Id = Guid.NewGuid(),
            UserId = callerUserId,
            Purpose = AccountCodePurpose.BiometricEnrolStepUp,
            Code = AccountCodeHasher.Hash(plaintext),
            CreatedAt = now,
            ExpiresAt = now.Add(StepUpLifetime),
        }, cancellationToken);

        await emailQueue.TryEnqueueAsync(
            await BuildBiometricStepUpEmailAsync(user.Email!, plaintext, cancellationToken),
            purpose: "BiometricEnrolStepUp",
            subjectEmail: user.Email!,
            subjectUserId: callerUserId,
            auditLog: auditLog,
            logger: logger,
            cancellationToken: cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.DeviceKeyStepUpIssued,
            Outcome = AuditOutcome.Success,
            ActorUserId = callerUserId,
            SubjectUserId = callerUserId,
            SubjectEmail = user.Email,
        }, cancellationToken);

        return new SendBiometricStepUpResponse(
            EmailMask.Mask(user.Email!), (int)StepUpLifetime.TotalSeconds);
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
        var now = timeProvider.SimfNow();
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
            || deviceKey.ChallengeExpiresAt <= timeProvider.SimfNow())
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

        // Consume the challenge so the same signature cannot be replayed. The
        // atomic conditional UPDATE (only the row still holding THIS challenge is
        // cleared) is the single-use gate: a concurrent replay within the window
        // clears nothing (affected == 0) and is rejected before any token mint.
        var consumedAt = timeProvider.SimfNow();
        var challengeConsumed = await identityDbContext.DeviceKeys
            .Where(k => k.Id == deviceKey.Id
                && k.CurrentChallenge == deviceKey.CurrentChallenge)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(k => k.CurrentChallenge, (string?)null)
                    .SetProperty(k => k.ChallengeExpiresAt, (DateTime?)null)
                    .SetProperty(k => k.LastUsedAt, (DateTime?)consumedAt),
                cancellationToken);
        if (challengeConsumed != 1)
        {
            await AuditFailureAsync(request.DeviceKeyId,
                ErrorCodes.DeviceKeyChallengeInvalid, "already_consumed",
                cancellationToken);
            return null;
        }

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

        deviceKey.RevokedAt = timeProvider.SimfNow();
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

    /// <summary>The token-mint path. itokenissuer-extraction — the claim set and
    /// the D-443 lifetime cap come from the shared <see cref="ITokenIssuer"/>;
    /// what stays here is the device-key-specific audit row. The <c>amr</c> claim
    /// is left to its <c>TwoFactorEnabled</c> derivation (null), exactly as this
    /// path did before: a signed challenge is a possession factor, not one of the
    /// three the flag describes.</summary>
    private async Task<AuthTokens> MintTokensAsync(
        SimfUser user, CancellationToken cancellationToken)
    {
        var tokens = await tokenIssuer.IssueAsync(
            user, secondFactorCompleted: null, cancellationToken);

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

        return tokens;
    }

    private async Task AuditFailureAsync(
        Guid deviceKeyId, string errorCode, string detail,
        CancellationToken cancellationToken)
    {
        await auditLog.WriteFailureAsync(
            AuditEvents.SignInWithDeviceKeyFailed,
            null,
            errorCode: errorCode,
            detail: $"deviceKeyId={deviceKeyId}; reason={detail}",
            cancellationToken: cancellationToken);
    }

    /// <summary>#7a — validates the emailed step-up code that must accompany a
    /// biometric enrolment and returns it (the caller consumes it only after the
    /// key is persisted). Returns null when the gate is disabled
    /// (<c>DeviceKey:RequireStepUpForEnrol=false</c>). Mirrors the sign-in OTP
    /// redemption: constant-time hash compare, expiry, and a per-code attempt
    /// cap that burns the code once the budget is spent. Single-use is
    /// best-effort (the frozen <c>AccountCode</c> has no concurrency token, so
    /// two simultaneous register calls with the same code could both pass — a
    /// pre-existing property of the whole AccountCode path, low blast radius
    /// since every key binds to the caller's own account and is revocable).</summary>
    private async Task<AccountCode?> ValidateEnrolStepUpAsync(
        Guid callerUserId, string? suppliedCode, DateTime now,
        CancellationToken cancellationToken)
    {
        if (!deviceKeyOptions.Value.RequireStepUpForEnrol)
        {
            return null;
        }

        var code = await accountCodes.GetLatestUnconsumedAsync(
            callerUserId, AccountCodePurpose.BiometricEnrolStepUp, cancellationToken);
        if (code is null || string.IsNullOrWhiteSpace(suppliedCode))
        {
            await AuditStepUpRejectedAsync(callerUserId,
                ErrorCodes.BiometricStepUpRequired, "missing", cancellationToken);
            throw new ApiException(
                ErrorCodes.BiometricStepUpRequired, 401,
                "A verification code is required to enable biometric sign-in.",
                "يلزم رمز تحقق لتفعيل تسجيل الدخول ببصمة الوجه.");
        }

        if (code.ExpiresAt <= now)
        {
            await accountCodes.TryConsumeAsync(code.Id, now, cancellationToken);
            await AuditStepUpRejectedAsync(callerUserId,
                ErrorCodes.BiometricStepUpInvalid, "expired", cancellationToken);
            throw new ApiException(
                ErrorCodes.BiometricStepUpInvalid, 401,
                "The verification code has expired. Request a new one.",
                "انتهت صلاحية رمز التحقق. اطلب رمزاً جديداً.");
        }

        if (!CodesMatch(code.Code, AccountCodeHasher.Hash(suppliedCode.Trim())))
        {
            // Burn the code once the attempt budget is spent so the 10^6 code
            // space can't be ground down across repeated register calls — the
            // increment and the burn are atomic so concurrent wrong tries can't
            // lose an increment and stretch the budget.
            var attempts = await accountCodes.IncrementAttemptCountAsync(
                code.Id, cancellationToken);
            if (attempts >= MaxStepUpAttempts)
            {
                await accountCodes.TryConsumeAsync(code.Id, now, cancellationToken);
            }
            await AuditStepUpRejectedAsync(callerUserId,
                ErrorCodes.BiometricStepUpInvalid, "mismatch", cancellationToken);
            throw new ApiException(
                ErrorCodes.BiometricStepUpInvalid, 401,
                "The verification code is incorrect.",
                "رمز التحقق غير صحيح.");
        }

        // Valid — the caller consumes it after the key is committed.
        return code;
    }

    private Task AuditStepUpRejectedAsync(
        Guid callerUserId, string errorCode, string reason,
        CancellationToken cancellationToken) =>
        auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.DeviceKeyStepUpRejected,
            Outcome = AuditOutcome.Failure,
            ActorUserId = callerUserId,
            SubjectUserId = callerUserId,
            ErrorCode = errorCode,
            Detail = $"reason={reason}",
        }, cancellationToken);

    /// <summary>Builds the bilingual step-up email; caller pairs it with
    /// <c>IEmailQueue.TryEnqueueAsync</c>.</summary>
    private Task<EmailMessage> BuildBiometricStepUpEmailAsync(
        string email, string code, CancellationToken cancellationToken) =>
        emailTemplates.RenderAsync(
            EmailTemplateType.BiometricStepUp, email,
            EmailTokens.ForCode(code, StepUpLifetime), cancellationToken);

    /// <summary>Compares the stored + supplied code hashes in constant time,
    /// so no timing side channel leaks.</summary>
    private static bool CodesMatch(string stored, string supplied) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(stored),
            Encoding.UTF8.GetBytes(supplied));

    private static DeviceKeyEntry ToEntry(DeviceKey deviceKey) =>
        new(deviceKey.Id, deviceKey.UserId, deviceKey.Algorithm,
            deviceKey.Label, deviceKey.CreatedAt, deviceKey.LastUsedAt,
            deviceKey.RevokedAt);
}
