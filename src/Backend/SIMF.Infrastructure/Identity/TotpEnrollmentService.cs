// Tests: SIMF.Api.Tests/TotpEnrolmentTests.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using OtpNet;
using QRCoder;
using SIMF.Application.Auditing;
using SIMF.Application.IdentityAccess;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Auditing;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// Implements authenticator-app enrolment (myComment #11, D-035). A user starts
/// enrolment with <see cref="SetupAsync"/>, scans the returned QR with their
/// authenticator, and confirms the first code with <see cref="ConfirmAsync"/>;
/// the staged secret then becomes the active one and
/// <c>TwoFactorEnabled</c> is turned on. <see cref="DisableAsync"/> reverses
/// the change after verifying a current code.
/// </summary>
internal sealed class TotpEnrollmentService(
    IUserAccountRepository accounts,
    ITotpVerifier totpVerifier,
    IRecoveryCodeService recoveryCodes,
    IAuditLog auditLog,
    ILogger<TotpEnrollmentService> logger) : ITotpEnrollmentService
{
    // The provider name + token name pair that ASP.NET Core Identity's
    // authenticator-key extension stores the active secret under.
    private const string AuthenticatorProvider = "[AspNetUserStore]";
    private const string ActiveSecretTokenName = "AuthenticatorKey";

    // SIMF's own staging slot for a secret that has been issued but not yet
    // confirmed by a first valid code. Kept under a SIMF-owned provider name so
    // it never collides with Identity's own storage.
    private const string SimfProvider = "[SIMF]";
    private const string PendingSecretTokenName = "PendingAuthenticatorKey";

    // The QR's otpauth issuer — what the authenticator app displays as the
    // account's source. Matches SIMF-VID-001.
    private const string Issuer = "SIMF";

    public async Task<TotpSetupResponse> SetupAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync(userId);

        // Twenty bytes is the RFC 4226 / RFC 6238 recommended secret length for
        // SHA-1 HMAC. Base32 is what authenticator apps expect.
        var secretBytes = KeyGeneration.GenerateRandomKey(20);
        var secret = Base32Encoding.ToString(secretBytes);

        // Stash the candidate secret — it becomes active only after a first
        // valid code confirms the user really has the authenticator paired.
        await accounts.SetAuthenticationTokenAsync(
            user, SimfProvider, PendingSecretTokenName, secret);

        var otpauthUri = BuildOtpAuthUri(user.Email!, secret);
        var qrSvg = BuildQrSvg(otpauthUri);

        await auditLog.WriteAsync(
            new AuditEntry
            {
                EventType = AuditEvents.TotpEnrolmentStarted,
                Outcome = AuditOutcome.Success,
                SubjectEmail = user.Email,
                SubjectUserId = user.Id,
            },
            cancellationToken);

        logger.LogInformation("TOTP enrolment started for {Email}", user.Email);
        return new TotpSetupResponse(secret, otpauthUri, qrSvg);
    }

    public async Task<TotpConfirmResponse> ConfirmAsync(
        Guid userId,
        TotpConfirmRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync(userId);

        var pending = await accounts.GetAuthenticationTokenAsync(
            user, SimfProvider, PendingSecretTokenName);
        if (string.IsNullOrWhiteSpace(pending))
        {
            await AuditFailure(AuditEvents.TotpEnrolmentFailed, user,
                ErrorCodes.TotpEnrolmentNotStarted, cancellationToken);
            throw new ApiException(
                ErrorCodes.TotpEnrolmentNotStarted, 400,
                "Start TOTP enrolment first.",
                "ابدأ تسجيل المصادقة الثنائية أولًا.");
        }

        var result = totpVerifier.Verify(pending, request.Code);
        if (!result.IsValid)
        {
            // Bind a wrong-code attempt to the account's lockout counter so a
            // brute-force burst lands the account in lockout after the
            // configured threshold — same posture as sign-in (D-038 follow-up
            // 5-agent review S1-1).
            await accounts.AccessFailedAsync(user);
            await AuditFailure(AuditEvents.TotpEnrolmentFailed, user,
                ErrorCodes.TotpEnrolmentCodeInvalid, cancellationToken,
                detail: "code-mismatch");
            throw new ApiException(
                ErrorCodes.TotpEnrolmentCodeInvalid, 400,
                "The verification code is not correct.",
                "رمز التحقق غير صحيح.");
        }
        await accounts.ResetAccessFailedCountAsync(user);

        // Promote the pending secret to active, turn 2FA on, and arm the
        // replay guard so a code re-played in the same time-step is rejected.
        await accounts.SetAuthenticationTokenAsync(
            user, AuthenticatorProvider, ActiveSecretTokenName, pending);
        await accounts.RemoveAuthenticationTokenAsync(
            user, SimfProvider, PendingSecretTokenName);
        user.LastUsedTotpTimestep = result.TimeStep;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await accounts.UpdateAsync(user);
        await accounts.SetTwoFactorEnabledAsync(user, true);

        // Mint the user's first batch of recovery codes — shown plaintext
        // exactly once in the response so the user can save them (D-040).
        var codes = await recoveryCodes.GenerateAsync(user.Id, cancellationToken);

        await auditLog.WriteAsync(
            new AuditEntry
            {
                EventType = AuditEvents.TotpEnrolmentConfirmed,
                Outcome = AuditOutcome.Success,
                SubjectEmail = user.Email,
                SubjectUserId = user.Id,
            },
            cancellationToken);
        await auditLog.WriteAsync(
            new AuditEntry
            {
                EventType = AuditEvents.TotpRecoveryCodesGenerated,
                Outcome = AuditOutcome.Success,
                SubjectEmail = user.Email,
                SubjectUserId = user.Id,
            },
            cancellationToken);

        logger.LogInformation("TOTP enrolment confirmed for {Email}", user.Email);
        return new TotpConfirmResponse(true, codes);
    }

    public async Task<TotpDisableResponse> DisableAsync(
        Guid userId,
        TotpDisableRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync(userId);

        if (!user.TwoFactorEnabled)
        {
            throw new ApiException(
                ErrorCodes.TotpNotEnabled, 400,
                "Two-factor authentication is not enabled for this account.",
                "المصادقة الثنائية غير مفعّلة لهذا الحساب.");
        }

        var active = await accounts.GetAuthenticatorKeyAsync(user);
        var result = totpVerifier.Verify(active ?? string.Empty, request.Code);
        if (!result.IsValid)
        {
            // Same brute-force gate as confirm — a wrong disable code counts
            // against the lockout budget so repeated guesses lock the account.
            await accounts.AccessFailedAsync(user);
            await AuditFailure(AuditEvents.TotpDisableFailed, user,
                ErrorCodes.AuthTotpInvalid, cancellationToken,
                detail: "code-mismatch");
            throw new ApiException(
                ErrorCodes.AuthTotpInvalid, 400,
                "The verification code is not correct.",
                "رمز التحقق غير صحيح.");
        }
        await accounts.ResetAccessFailedCountAsync(user);

        // Turn 2FA off and remove the active secret — a re-enrolment then
        // starts cleanly from a fresh QR. Wipe the recovery-code batch too
        // (D-040): codes only exist while 2FA is on; leaving them behind would
        // let a leaked code re-enable bypass after the user thought they were
        // safe.
        await accounts.SetTwoFactorEnabledAsync(user, false);
        await accounts.RemoveAuthenticationTokenAsync(
            user, AuthenticatorProvider, ActiveSecretTokenName);
        user.LastUsedTotpTimestep = null;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await accounts.UpdateAsync(user);
        await recoveryCodes.RevokeAllAsync(user.Id, cancellationToken);

        await auditLog.WriteAsync(
            new AuditEntry
            {
                EventType = AuditEvents.TotpDisabled,
                Outcome = AuditOutcome.Success,
                SubjectEmail = user.Email,
                SubjectUserId = user.Id,
            },
            cancellationToken);

        logger.LogInformation("TOTP disabled for {Email}", user.Email);
        return new TotpDisableResponse(false);
    }

    private async Task<SimfUser> GetUserAsync(Guid userId)
    {
        var user = await accounts.FindByIdAsync(userId);
        return user ?? throw new ApiException(
            ErrorCodes.AuthAccountNotFound, 404,
            "The account was not found.",
            "لم يتم العثور على الحساب.");
    }

    /// <summary>
    /// Builds the standard <c>otpauth://</c> URI an authenticator app reads
    /// (RFC 6238 + the Google Authenticator key URI convention).
    /// </summary>
    private static string BuildOtpAuthUri(string email, string secret)
    {
        var label = Uri.EscapeDataString($"{Issuer}:{email}");
        var issuer = Uri.EscapeDataString(Issuer);
        return $"otpauth://totp/{label}?secret={secret}&issuer={issuer}"
            + "&algorithm=SHA1&digits=6&period=30";
    }

    private static string BuildQrSvg(string otpauthUri)
    {
        // ECCLevel.M (medium, ~15% error correction) gives a smaller matrix
        // than Q for the same payload — fewer modules means a smaller SVG
        // text. pixelsPerModule: 4 keeps the rendered size readable while
        // halving the SVG byte count compared to 6. The page sizes the SVG
        // up via CSS (.simf-qr svg { inline-size: 224px }).
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(otpauthUri, QRCodeGenerator.ECCLevel.M);
        return new SvgQRCode(data).GetGraphic(
            pixelsPerModule: 4,
            darkColorHex: "#0B2545",
            lightColorHex: "#FFFFFF",
            drawQuietZones: true);
    }

    private Task AuditFailure(
        string eventType,
        SimfUser user,
        string errorCode,
        CancellationToken cancellationToken,
        string? detail = null) =>
        auditLog.WriteAsync(
            new AuditEntry
            {
                EventType = eventType,
                Outcome = AuditOutcome.Failure,
                SubjectEmail = user.Email,
                SubjectUserId = user.Id,
                ErrorCode = errorCode,
                Detail = detail,
            },
            cancellationToken);
}
